namespace DocumentAPI.Observability;

using DocumentAPI.Options;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Enriches emitted telemetry with API-specific metadata.
/// </summary>
public sealed class DocumentApiTelemetryInitializer(
    IHttpContextAccessor httpContextAccessor,
    IOptions<DocumentApiOptions> options) : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly string _cloudRoleName = options.Value.Monitoring.ApplicationInsights.CloudRoleName;

    /// <summary>
    /// Initializes the specified telemetry item with API-specific context.
    /// </summary>
    /// <param name="telemetry">The telemetry item to enrich.</param>
    public void Initialize(ITelemetry telemetry)
    {
        if (string.IsNullOrWhiteSpace(telemetry.Context.Cloud.RoleName))
        {
            telemetry.Context.Cloud.RoleName = string.IsNullOrWhiteSpace(_cloudRoleName)
                ? "DocumentAPI"
                : _cloudRoleName;
        }

        if (telemetry is not ISupportProperties properties || _httpContextAccessor.HttpContext is not { } httpContext)
        {
            return;
        }

        var correlationId = ResolveCorrelationId(httpContext);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            properties.Properties[CorrelationIdMiddleware.HeaderName] = correlationId;
        }

        properties.Properties["ServiceName"] = "DocumentAPI";
    }

    /// <summary>
    /// Resolves the correlation identifier from the current HTTP context.
    /// </summary>
    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (httpContext.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out StringValues responseValues)
            && !StringValues.IsNullOrEmpty(responseValues))
        {
            return responseValues.ToString();
        }

        if (httpContext.Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out StringValues requestValues)
            && !StringValues.IsNullOrEmpty(requestValues))
        {
            return requestValues.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}