namespace DocumentAPI.Options;

/// <summary>
/// Represents the settings required to store document binary content.
/// </summary>
public sealed class DocumentStorageOptions
{
    /// <summary>
    /// Gets or sets the Blob service URI used when Azure Blob Storage is enabled.
    /// </summary>
    public string ServiceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the blob container name used for document content.
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;
}