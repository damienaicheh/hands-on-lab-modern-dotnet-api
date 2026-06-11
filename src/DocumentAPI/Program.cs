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
// <lab id="7">
//|// TODO Lab 7: Register the in-memory cache service used by document search.
builder.Services.AddMemoryCache();
// </lab>
// <lab id="12">
//|// TODO Lab 12: Register HTTP logging and include the correlation id header.
builder.Services.AddHttpLogging(options =>
{
	options.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
	options.RequestHeaders.Add(CorrelationIdMiddleware.HeaderName);
	options.ResponseHeaders.Add(CorrelationIdMiddleware.HeaderName);
});
// </lab>
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
// <lab id="11">
//|// TODO Lab 11: Register JWT bearer authentication and configure token validation.
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
// </lab>

// <lab id="12">
//|builder.Services.AddHttpContextAccessor();
//|builder.Services.AddApplicationInsightsTelemetry();
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
// </lab>

// <lab id="10">
//|// TODO Lab 10: Register API versioning and expose version metadata for Swagger.
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
// </lab>

// <lab id="1">
//|// TODO Lab 1: Register the minimal Swagger/OpenAPI services.
builder.Services.AddEndpointsApiExplorer();
// <lab id="10">
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
// </lab>
builder.Services.AddSwaggerGen(options =>
{
// <lab id="11">
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
// </lab>

	// Include XML comments if available for better Swagger documentation.
	var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);

	if (File.Exists(xmlPath))
	{
		options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
	}

// <lab id="10">
	options.OperationFilter<SwaggerDefaultValues>();
// </lab>
});
// </lab>

builder.Services.AddDocumentServices(documentApiOptions);

var app = builder.Build();

await app.Services.InitializeDocumentDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
// <lab id="10">
	var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
// </lab>

	// <lab id="1">
	//|// TODO Lab 1: Expose Swagger and Swagger UI in the Development environment.
	app.UseSwagger();
	// <lab id="10">
	//|app.UseSwaggerUI();
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
	// </lab>
	// </lab>
}

// <lab id="12">
//|// TODO Lab 12: Enable HTTP logging and correlation id middleware.
app.UseHttpLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
// </lab>
// <lab id="11">
//|// TODO Lab 11: Enable authentication before authorization.
app.UseAuthentication();
app.UseAuthorization();
// </lab>

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
