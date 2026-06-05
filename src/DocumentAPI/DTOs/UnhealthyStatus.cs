namespace DocumentAPI.DTOs;

/// <summary>
/// Represents a health response when the service is unavailable.
/// </summary>
public sealed record UnhealthyStatus
{
    /// <summary>
    /// Gets the current health status value.
    /// </summary>
    public required string Status { get; init; }
}