using System.Reflection;
using System.Text;
using DocumentAPI.Endpoints;
using DocumentAPI.Models;
using DocumentAPI.Observability;
using DocumentAPI.Options;
using DocumentAPI.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var documentApiOptions = builder.Configuration.GetSection(DocumentApiOptions.SectionName).Get<DocumentApiOptions>() ?? new DocumentApiOptions();

builder.Services.Configure<DocumentApiOptions>(builder.Configuration.GetSection(DocumentApiOptions.SectionName));
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
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = false;
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
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsJsonAsync(new UnauthorizedError
				{
					Code = "UNAUTHORIZED",
					Message = "Access is unauthorized.",
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

builder.Services.AddEndpointsApiExplorer();
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

	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "DocumentAPI",
		Version = "v1",
		Description = "Document management API built with .NET 10 Minimal APIs.",
	});

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
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentAPI v1");
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
