namespace DocumentAPI.Services.Validators.Documents;

using DocumentAPI.DTOs;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Defines validation rules for multipart document uploads.
/// </summary>
public interface IDocumentUploadValidator
{
    /// <summary>
    /// Validates an uploaded file and metadata payload.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="metadata">The uploaded metadata.</param>
    /// <returns>A validation failure when the payload is invalid; otherwise <see langword="null" />.</returns>
    RequestValidationFailure? Validate(IFormFile? file, DocumentMetadataDto? metadata);
}
