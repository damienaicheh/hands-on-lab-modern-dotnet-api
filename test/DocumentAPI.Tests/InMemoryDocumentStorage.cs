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
    public Task<string> SaveAsync(string documentId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName));
        var storageKey = string.IsNullOrWhiteSpace(extension)
            ? documentId
            : $"{documentId}{extension.ToLowerInvariant()}";

        _documents[storageKey] = [.. content];
        return Task.FromResult(storageKey);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        _documents.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (!_documents.TryGetValue(storageKey, out var content))
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