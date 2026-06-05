namespace DocumentAPI.DTOs;

/// <summary>
/// Represents the error payload returned when access is unauthorized.
/// </summary>
public sealed record UnauthorizedError
{
    /// <summary>
    /// Gets the string error code associated with the unauthorized response.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }
}