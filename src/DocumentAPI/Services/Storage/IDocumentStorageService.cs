namespace DocumentAPI.Services.Storage;

/// <summary>
/// Defines the contract used to persist and retrieve document binary content.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Persists document binary content using its deterministic content hash as the blob identifier.
    /// </summary>
    /// <param name="contentHash">The deterministic content hash used as the blob identifier.</param>
    /// <param name="content">The file content stream to upload.</param>
    /// <param name="md5Hash">The pre-computed MD5 hash of the content for integrity verification.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes document binary content from the underlying store.
    /// </summary>
    Task DeleteAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a read stream for previously stored binary content.
    /// </summary>
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the underlying content store can be reached.
    /// </summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
