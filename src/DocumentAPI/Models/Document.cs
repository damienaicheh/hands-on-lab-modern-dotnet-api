namespace DocumentAPI.Models;

/// <summary>
/// Represents a document and its metadata persisted in the database.
/// </summary>
public sealed class Document : EntityBase
{
    /// <summary>
    /// Gets or sets the original file name of the document.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the document.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size of the document in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the user-facing title of the document.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the descriptive summary of the document.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the source system or origin of the document.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the tags associated with the document.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the deterministic content hash used to detect duplicates.
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the storage key used to retrieve the binary content.
    /// </summary>
    public required string StorageKey { get; set; }
}
