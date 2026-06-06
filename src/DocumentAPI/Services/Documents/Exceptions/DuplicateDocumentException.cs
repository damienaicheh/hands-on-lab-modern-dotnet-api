namespace DocumentAPI.Services.Documents.Exceptions;

/// <summary>
/// Represents an upload failure caused by an already existing document with the same content.
/// </summary>
/// <param name="existingDocumentId">The identifier of the existing matching document.</param>
public sealed class DuplicateDocumentException(string existingDocumentId)
    : Exception("A document with the same content already exists.")
{
    /// <summary>
    /// Gets the identifier of the existing matching document.
    /// </summary>
    public string ExistingDocumentId { get; } = existingDocumentId;
}
