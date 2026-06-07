namespace DocumentAPI.Endpoints;

using Microsoft.AspNetCore.Mvc;

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
    /// <returns>A <see cref="ProblemDetails" /> validation error when the parameter is missing; otherwise <see langword="null" />.</returns>
    public static ProblemDetails? Validate(string? apiVersion)
    {
        return string.IsNullOrWhiteSpace(apiVersion)
            ? new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = MissingParameterMessage,
            }
            : null;
    }
}