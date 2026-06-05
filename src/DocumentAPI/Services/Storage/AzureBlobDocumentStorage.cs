namespace DocumentAPI.Services.Storage;

using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentAPI.Options;
using DocumentAPI.Services.Identity;
using Microsoft.Extensions.Options;

/// <summary>
/// Stores and retrieves document content from Azure Blob Storage.
/// </summary>
public sealed class AzureBlobDocumentStorage : IDocumentStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobDocumentStorage" /> class.
    /// </summary>
    /// <param name="options">The bound document API options.</param>
    public AzureBlobDocumentStorage(IOptions<DocumentApiOptions> options)
    {
        var storageOptions = options.Value.Storage;
        var credential = AzureIdentityCredentialFactory.Create(storageOptions.ManagedIdentityClientId);
        var blobOptions = new BlobClientOptions
        {
            Retry =
            {
                Delay = TimeSpan.FromSeconds(2),
                MaxRetries = 5,
                Mode = RetryMode.Exponential,
                MaxDelay = TimeSpan.FromSeconds(10),
                NetworkTimeout = TimeSpan.FromSeconds(100),
            },
        };
        var blobServiceClient = new BlobServiceClient(new Uri(storageOptions.ServiceUri), credential, blobOptions);
        _containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(string documentId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        var storageKey = CreateStorageKey(documentId, fileName);
        var blobClient = _containerClient.GetBlobClient(storageKey);

        var expectedHash = ComputeMd5(content);

        await using var stream = new MemoryStream(content, writable: false);
        var response = await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(fileName) ? "application/octet-stream" : null,
                    ContentHash = expectedHash,
                },
            },
            cancellationToken);

        await VerifyContentIntegrityAsync(blobClient, storageKey, expectedHash, response.Value.ContentHash, cancellationToken);

        return storageKey;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _containerClient.DeleteBlobIfExistsAsync(storageKey, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            return await _containerClient.GetBlobClient(storageKey).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensures that the target blob container exists before any storage operation runs.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Computes the MD5 hash of the document content used to verify storage integrity.
    /// </summary>
    private static byte[] ComputeMd5(byte[] content)
    {
        // MD5 is used solely to satisfy the Content-MD5 integrity contract of Azure Blob Storage, not for security.
#pragma warning disable CA5351
        return MD5.HashData(content);
#pragma warning restore CA5351
    }

    /// <summary>
    /// Verifies that the MD5 hash reported by Azure Blob Storage matches the locally computed hash and removes
    /// the blob when a mismatch indicates the content was corrupted in transit.
    /// </summary>
    private static async Task VerifyContentIntegrityAsync(
        BlobClient blobClient,
        string storageKey,
        byte[] expectedHash,
        byte[]? storedHash,
        CancellationToken cancellationToken)
    {
        if (storedHash is not null && CryptographicOperations.FixedTimeEquals(expectedHash, storedHash))
        {
            return;
        }

        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        throw new DocumentStorageIntegrityException(storageKey, expectedHash, storedHash);
    }

    /// <summary>
    /// Creates the blob storage key from the document identifier and file extension.
    /// </summary>
    private static string CreateStorageKey(string documentId, string fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName));
        return string.IsNullOrWhiteSpace(extension)
            ? documentId
            : $"{documentId}{extension.ToLowerInvariant()}";
    }
}