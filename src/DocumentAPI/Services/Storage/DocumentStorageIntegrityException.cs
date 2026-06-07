namespace DocumentAPI.Services.Storage;

/// <summary>
/// Represents a storage failure caused by a mismatch between the locally computed MD5 hash and the hash
/// reported by Azure Blob Storage, indicating that the document content may have been corrupted in transit.
/// </summary>
/// <param name="contentHash">The content hash used as the blob identifier.</param>
/// <param name="expectedHash">The MD5 hash computed locally from the document content.</param>
/// <param name="actualHash">The MD5 hash reported by Azure Blob Storage, when available.</param>
public sealed class DocumentStorageIntegrityException(string contentHash, byte[] expectedHash, byte[]? actualHash)
    : Exception(BuildMessage(contentHash, expectedHash, actualHash))
{
    /// <summary>
    /// Gets the content hash used as the blob identifier.
    /// </summary>
    public string ContentHash { get; } = contentHash;

    /// <summary>
    /// Gets the MD5 hash computed locally from the document content.
    /// </summary>
    public byte[] ExpectedHash { get; } = expectedHash;

    /// <summary>
    /// Gets the MD5 hash reported by Azure Blob Storage, when available.
    /// </summary>
    public byte[]? ActualHash { get; } = actualHash;

    /// <summary>
    /// Builds a descriptive message comparing the expected and reported MD5 hashes.
    /// </summary>
    private static string BuildMessage(string contentHash, byte[] expectedHash, byte[]? actualHash)
    {
        var actual = actualHash is null ? "<none>" : Convert.ToHexString(actualHash);
        return $"Content integrity verification failed for content hash '{contentHash}'. " +
            $"Expected MD5 '{Convert.ToHexString(expectedHash)}' but the storage service reported '{actual}'.";
    }
}
