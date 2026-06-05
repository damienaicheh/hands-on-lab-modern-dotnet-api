namespace DocumentAPI.Options;

/// <summary>
/// Defines the supported providers for storing document binary content.
/// </summary>
public enum DocumentStorageProvider
{
    /// <summary>
    /// Stores document content on the local file system.
    /// </summary>
    LocalFile,

    /// <summary>
    /// Stores document content in Azure Blob Storage.
    /// </summary>
    AzureBlob,
}