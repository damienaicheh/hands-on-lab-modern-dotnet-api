namespace DocumentAPI.Services.Documents;

using DocumentAPI.DTOs;
using DocumentAPI.Options;
using Microsoft.AspNetCore.Http;
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
        if (file is null)
        {
            return new RequestValidationFailure(
                StatusCodes.Status400BadRequest,
                new ApiError { Code = 400, Message = "The file part is required." });
        }

        if (metadata is null)
        {
            return new RequestValidationFailure(
                StatusCodes.Status400BadRequest,
                new ApiError { Code = 400, Message = "The metadata part is required." });
        }

        if (file.Length <= 0)
        {
            return new RequestValidationFailure(
                StatusCodes.Status400BadRequest,
                new ApiError { Code = 400, Message = "The uploaded file cannot be empty." });
        }

        if (file.Length > _options.Upload.MaxFileSizeBytes)
        {
            return new RequestValidationFailure(
                StatusCodes.Status413PayloadTooLarge,
                new ApiError
                {
                    Code = 413,
                    Message = $"The uploaded file exceeds the configured maximum size of {_options.Upload.MaxFileSizeBytes} bytes.",
                });
        }

        if (!DocumentContentTypes.IsSupported(file.ContentType))
        {
            return new RequestValidationFailure(
                StatusCodes.Status400BadRequest,
                new ApiError
                {
                    Code = 400,
                    Message = "Unsupported content type. Allowed values are PDF, TXT, DOC, and DOCX.",
                });
        }

        return null;
    }
}