namespace DocumentAPI.Validators;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Represents the result of request payload validation.
/// </summary>
/// <param name="Problem">The <see cref="ProblemDetails" /> payload describing the validation failure.</param>
public sealed record RequestValidationFailure(ProblemDetails Problem);
