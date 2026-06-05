namespace DocumentAPI.DTOs;

/// <summary>
/// Represents a document returned by the API.
/// </summary>
public sealed record DocumentDto
{
    /// <summary>
    /// Gets the unique document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the original file name of the document.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the MIME content type of the document.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the size of the document in bytes.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Gets the document metadata payload.
    /// </summary>
    public DocumentMetadataDto? Metadata { get; init; }
}