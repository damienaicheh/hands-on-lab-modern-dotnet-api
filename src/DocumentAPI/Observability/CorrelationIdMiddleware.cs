namespace DocumentAPI.Observability;

using Microsoft.Extensions.Primitives;

/// <summary>
/// Ensures that every request and response carries a correlation identifier.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>
    /// Gets the HTTP header used to exchange correlation identifiers.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next = next;
    private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

    /// <summary>
    /// Processes the current request and ensures a correlation identifier is available.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value,
        });

        await _next(context);
    }

    /// <summary>
    /// Resolves the correlation identifier from the request headers or generates a new one.
    /// </summary>
    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(HeaderName, out StringValues values) && !StringValues.IsNullOrEmpty(values))
        {
            return values.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}