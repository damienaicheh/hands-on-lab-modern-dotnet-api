namespace DocumentAPI.Models;

/// <summary>
/// Represents a generic API error response.
/// </summary>
public sealed record ApiError
{
    /// <summary>
    /// Gets the numeric error code returned by the API.
    /// </summary>
    public required int Code { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }
}