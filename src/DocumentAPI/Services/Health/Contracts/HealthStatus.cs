namespace DocumentAPI.Services.Health.Contracts;

/// <summary>
/// Defines the supported health status values.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The service or dependency is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// The service is available but at least one dependency is unhealthy.
    /// </summary>
    Degraded,

    /// <summary>
    /// The service or dependency is unavailable.
    /// </summary>
    Unhealthy,
}