namespace DocumentAPI.Services.Health.Contracts;

/// <summary>
/// Represents the health state of a single dependency.
/// </summary>
/// <param name="Status">The dependency health status label.</param>
/// <param name="Description">Optional detail describing why the dependency is degraded or unhealthy.</param>
public sealed record HealthDependencyState(string Status, string? Description = null);
