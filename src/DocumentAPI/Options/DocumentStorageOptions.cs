namespace DocumentAPI.Options;

/// <summary>
/// Represents the settings required to store document binary content.
/// </summary>
public sealed class DocumentStorageOptions
{
    /// <summary>
    /// Gets or sets the storage provider used for document content.
    /// </summary>
    public DocumentStorageProvider Provider { get; set; } = DocumentStorageProvider.LocalFile;

    /// <summary>
    /// Gets or sets the local root path used when the local file provider is enabled.
    /// </summary>
    public string LocalRootPath { get; set; } = "App_Data/documents";

    /// <summary>
    /// Gets or sets the Blob service URI used when Azure Blob Storage is enabled.
    /// </summary>
    public string ServiceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the blob container name used for document content.
    /// </summary>
    public string ContainerName { get; set; } = "documents";

    /// <summary>
    /// Gets or sets the optional client identifier of the user-assigned managed identity used for Azure Blob access.
    /// </summary>
    public string ManagedIdentityClientId { get; set; } = string.Empty;
}