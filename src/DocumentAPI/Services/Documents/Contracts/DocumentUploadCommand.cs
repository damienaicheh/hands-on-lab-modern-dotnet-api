namespace DocumentAPI.Services.Documents;

using DocumentAPI.DTOs;

/// <summary>
/// Represents the payload required to upload a new document.
/// </summary>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The MIME content type of the file.</param>
/// <param name="Content">The file content stream. Must support seeking.</param>
/// <param name="Length">The byte length of the file content.</param>
/// <param name="Metadata">The document metadata payload.</param>
public sealed record DocumentUploadCommand(string FileName, string ContentType, Stream Content, long Length, DocumentMetadataDto Metadata);
