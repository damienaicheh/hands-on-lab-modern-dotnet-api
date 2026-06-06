namespace DocumentAPI.Services.Documents;

using DocumentAPI.DTOs;

/// <summary>
/// Represents the payload required to upload a new document.
/// </summary>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The MIME content type of the file.</param>
/// <param name="Content">The binary file content.</param>
/// <param name="Metadata">The document metadata payload.</param>
public sealed record DocumentUploadCommand(string FileName, string ContentType, byte[] Content, DocumentMetadataDto Metadata);
