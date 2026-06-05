namespace DocumentAPI.Services.Storage;

using DocumentAPI.Options;
using Microsoft.Extensions.Options;

/// <summary>
/// Stores document content on the local file system.
/// </summary>
/// <param name="options">The bound document API options.</param>
/// <param name="environment">The current host environment.</param>
public sealed class LocalFileDocumentStorage(IOptions<DocumentApiOptions> options, IHostEnvironment environment) : IDocumentStorage
{
    private readonly string _storageRoot = Path.GetFullPath(options.Value.Storage.LocalRootPath, environment.ContentRootPath);

    /// <inheritdoc />
    public async Task<string> SaveAsync(string documentId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storageRoot);

        var extension = Path.GetExtension(Path.GetFileName(fileName));
        var storageKey = string.IsNullOrWhiteSpace(extension)
            ? documentId
            : $"{documentId}{extension.ToLowerInvariant()}";
        var path = Path.Combine(_storageRoot, storageKey);

        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return storageKey;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_storageRoot, storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_storageRoot, storageKey);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_storageRoot);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}