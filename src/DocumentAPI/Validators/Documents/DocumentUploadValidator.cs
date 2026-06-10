namespace DocumentAPI.Validators.Documents;

using DocumentAPI.DTOs;
using DocumentAPI.Options;
using DocumentAPI.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Validates document uploads against supported content types and size limits.
/// </summary>
/// <param name="options">The bound document API options.</param>
public sealed class DocumentUploadValidator(IOptions<DocumentApiOptions> options) : IDocumentUploadValidator
{
    private readonly DocumentApiOptions _options = options.Value;

    /// <inheritdoc />
    public RequestValidationFailure? Validate(IFormFile? file, DocumentMetadataDto? metadata)
    {
        // <lab id="5">
        //|        return null;
        if (file is null)
        {
            return new RequestValidationFailure(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The file part is required.",
                });
        }

        if (metadata is null)
        {
            return new RequestValidationFailure(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The metadata part is required.",
                });
        }

        if (file.Length <= 0)
        {
            return new RequestValidationFailure(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The uploaded file cannot be empty.",
                });
        }

        if (file.Length > _options.Upload.MaxFileSizeBytes)
        {
            return new RequestValidationFailure(
                new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Payload Too Large",
                    Detail = $"The uploaded file exceeds the configured maximum size of {_options.Upload.MaxFileSizeBytes} bytes.",
                });
        }

        if (!DocumentContentTypes.IsSupported(file.ContentType))
        {
            return new RequestValidationFailure(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "Unsupported content type. Allowed values are PDF, TXT, DOC, and DOCX.",
                });
        }

        return null;
        // </lab>
    }
}