namespace DocumentAPI.Options;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents the settings required to store document binary content.
/// </summary>
public sealed class DocumentStorageOptions
{
    /// <summary>
    /// Gets or sets the Blob service URI used when Azure Blob Storage is enabled.
    /// </summary>
    [Required]
    public string ServiceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the blob container name used for document content.
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = string.Empty;
}