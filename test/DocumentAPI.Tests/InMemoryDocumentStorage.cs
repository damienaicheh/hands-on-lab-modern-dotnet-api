namespace DocumentAPI.Tests;

using System.Collections.Concurrent;
using DocumentAPI.Services.Storage;

/// <summary>
/// In-memory document storage used by integration tests to avoid external blob dependencies.
/// </summary>
internal sealed class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SaveAsync(string contentHash, byte[] content, CancellationToken cancellationToken)
    {
        _documents[contentHash] = [.. content];
        return Task.CompletedTask;
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