namespace DocumentAPI.DTOs;

/// <summary>
/// Represents the metadata associated with a document.
/// </summary>
public sealed record DocumentMetadataDto
{
    /// <summary>
    /// Gets the user-facing title of the document.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the descriptive summary of the document.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the tags associated with the document.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Gets the source system or origin of the document.
    /// </summary>
    public string? Source { get; init; }
}