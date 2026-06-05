namespace DocumentAPI.DTOs;

/// <summary>
/// Represents a health response when the service is available.
/// </summary>
public sealed record HealthyOrDegradedStatus
{
    /// <summary>
    /// Gets the current health status value.
    /// </summary>
    public required string Status { get; init; }
}