namespace DocumentAPI.Endpoints;

using DocumentAPI.DTOs;

/// <summary>
/// Centralizes the API version query parameter contract used by the endpoints.
/// </summary>
internal static class ApiVersionValidation
{
    /// <summary>
    /// Gets the required API version query parameter name.
    /// </summary>
    public const string ParameterName = "api-version";

    private const string MissingParameterMessage = "The api-version query parameter is required.";

    /// <summary>
    /// Validates that the required API version query parameter is present.
    /// </summary>
    /// <param name="apiVersion">The requested API version.</param>
    /// <returns>A validation error when the parameter is missing; otherwise <see langword="null" />.</returns>
    public static ApiError? Validate(string? apiVersion)
    {
        return string.IsNullOrWhiteSpace(apiVersion)
            ? new ApiError { Code = 400, Message = MissingParameterMessage }
            : null;
    }
}