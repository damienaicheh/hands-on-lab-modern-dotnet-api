namespace DocumentAPI.Endpoints;

using System.Linq;
using DocumentAPI.Models;
using DocumentAPI.Services.Health;

/// <summary>
/// Registers the health-related Minimal API endpoints.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps the service health endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The original endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", GetHealthAsync)
            .WithName("Health_check")
            .WithTags("Health")
            .AllowAnonymous()
            .Produces<HealthyOrDegradedStatus>(StatusCodes.Status200OK)
            .Produces<UnhealthyStatus>(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    /// <summary>
    /// Returns the current health status of the service.
    /// </summary>
    private static async Task<IResult> GetHealthAsync(
        IHealthStatusService healthStatusService,
        CancellationToken cancellationToken)
    {
        var status = await healthStatusService.GetStatusAsync(cancellationToken);

        if (!status.IsAvailable)
        {
            return Results.Json(new UnhealthyStatus { Status = status.Status }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!string.Equals(status.Status, "Degraded", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new HealthyOrDegradedStatus { Status = status.Status });
        }

        return Results.Ok(new HealthyOrDegradedStatus
        {
            Status = status.Status,
            Checks = status.Checks.ToDictionary(
                pair => pair.Key,
                pair => new HealthCheckStatus
                {
                    Status = pair.Value.Status,
                    Description = pair.Value.Description,
                },
                StringComparer.Ordinal),
        });
    }
}
