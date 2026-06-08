namespace DocumentAPI.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a health response when the service is available.
/// </summary>
public sealed record HealthyOrDegradedStatus
{
    /// <summary>
    /// Gets the current health status value.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the optional per-dependency checks when the service is degraded.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, HealthCheckStatus>? Checks { get; init; }
}