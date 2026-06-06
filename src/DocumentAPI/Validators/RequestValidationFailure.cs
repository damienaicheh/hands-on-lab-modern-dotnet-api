namespace DocumentAPI.Services.Validators;

using DocumentAPI.Models;

/// <summary>
/// Represents the result of request payload validation.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return.</param>
/// <param name="Error">The error payload to return.</param>
public sealed record RequestValidationFailure(int StatusCode, ApiError Error);
