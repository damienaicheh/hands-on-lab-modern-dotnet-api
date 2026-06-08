namespace DocumentAPI.Tests;

using System.Collections.Concurrent;
using DocumentAPI.Services.Storage;

/// <summary>
/// In-memory document storage used by integration tests to avoid external blob dependencies.
/// </summary>
internal sealed class InMemoryDocumentStorage : IDocumentStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _documents[contentHash] = buffer.ToArray();
    }

    /// <inheritdoc />
    public Task DeleteAsync(string contentHash, CancellationToken cancellationToken)
    {
        _documents.TryRemove(contentHash, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken)
    {
        if (!_documents.TryGetValue(contentHash, out var content))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new MemoryStream([.. content], writable: false);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}