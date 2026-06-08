using System.Reflection;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using DocumentAPI.Endpoints;
using DocumentAPI.OpenApi;
using DocumentAPI.Observability;
using DocumentAPI.Options;
using DocumentAPI.Services;
using DocumentAPI.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

const string ProblemJsonContentType = "application/problem+json";

var documentApiOptions = builder.Configuration.GetSection(DocumentApiOptions.SectionName).Get<DocumentApiOptions>() ?? new DocumentApiOptions();

builder.Services.AddDocumentApiOptions(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddHttpLogging(options =>
{
	options.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
	options.RequestHeaders.Add(CorrelationIdMiddleware.HeaderName);
	options.ResponseHeaders.Add(CorrelationIdMiddleware.HeaderName);
});
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = documentApiOptions.Authentication.RequireHttpsMetadata;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = documentApiOptions.Authentication.Issuer,
			ValidateAudience = true,
			ValidAudience = documentApiOptions.Authentication.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(documentApiOptions.Authentication.SigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1),
		};
		options.Events = new JwtBearerEvents
		{
			OnChallenge = async context =>
			{
				context.HandleResponse();
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				context.Response.ContentType = ProblemJsonContentType;
				await context.Response.WriteAsJsonAsync(new ProblemDetails
				{
					Status = StatusCodes.Status401Unauthorized,
					Title = "Unauthorized",
					Detail = "Access is unauthorized.",
				});
			},
		};
	});

var applicationInsightsOptions = documentApiOptions.ApplicationInsights;
var applicationInsightsConnectionString = applicationInsightsOptions.Enabled
	? ResolveApplicationInsightsConnectionString(builder.Configuration, applicationInsightsOptions)
	: null;

if (applicationInsightsOptions.Enabled && string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
	throw new InvalidOperationException("Application Insights is enabled but no connection string was configured.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITelemetryInitializer, DocumentApiTelemetryInitializer>();
builder.Services.AddApplicationInsightsTelemetry(options =>
{
	options.ConnectionString = applicationInsightsConnectionString;
	options.EnableAdaptiveSampling = applicationInsightsOptions.EnableAdaptiveSampling;
});

builder.Services
	.AddApiVersioning(options =>
	{
		options.AssumeDefaultVersionWhenUnspecified = false;
		options.ReportApiVersions = true;
		options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
	})
	.AddApiExplorer(options =>
	{
		options.GroupNameFormat = "'v'V";
	});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(options =>
{
	var bearerSecurityScheme = new OpenApiSecurityScheme
	{
		Name = "Authorization",
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		In = ParameterLocation.Header,
		Description = "Provide a valid JWT bearer token.",
	};

	options.AddSecurityDefinition("Bearer", bearerSecurityScheme);

	options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("Bearer", hostDocument: document, externalResource: null)] = [],
	});

	// Include XML comments if available for better Swagger documentation.
	var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);

	if (File.Exists(xmlPath))
	{
		options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
	}

	options.OperationFilter<SwaggerDefaultValues>();
});

builder.Services.AddDocumentServices(documentApiOptions);

var app = builder.Build();

await app.Services.InitializeDocumentDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
	var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
		{
			options.SwaggerEndpoint(
				$"/swagger/{description.GroupName}/swagger.json",
				$"DocumentAPI {description.GroupName.ToUpperInvariant()}");
		}

		options.RoutePrefix = "swagger";
	});
}

app.UseHttpLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapDocumentEndpoints();

app.Run();

// Resolves the Application Insights connection string from strongly typed options or environment-based configuration.
static string? ResolveApplicationInsightsConnectionString(IConfiguration configuration, ApplicationInsightsMonitoringOptions options)
{
	if (!string.IsNullOrWhiteSpace(options.ConnectionString))
	{
		return options.ConnectionString;
	}

	return configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
		?? configuration["ApplicationInsights:ConnectionString"];
}

/// <summary>
/// Provides an entry point type for integration tests and tooling.
/// </summary>
public partial class Program;
