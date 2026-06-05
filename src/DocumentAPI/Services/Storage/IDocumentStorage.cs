namespace DocumentAPI.Services.Storage;

/// <summary>
/// Defines the contract used to persist and retrieve document binary content.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Persists document binary content and returns the storage key used to retrieve it later.
    /// </summary>
    Task<string> SaveAsync(string documentId, string fileName, byte[] content, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes document binary content from the underlying store.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a read stream for previously stored binary content.
    /// </summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the underlying content store can be reached.
    /// </summary>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
