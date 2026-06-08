namespace DocumentAPI.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents the health status details of a single dependency.
/// </summary>
public sealed record HealthCheckStatus
{
    /// <summary>
    /// Gets the health status value for the dependency.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the optional diagnostic description for the dependency status.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
