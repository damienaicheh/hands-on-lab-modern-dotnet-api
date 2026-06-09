namespace DocumentAPI.Services.Storage;

using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocumentAPI.Options;
using Microsoft.Extensions.Options;

/// <summary>
/// Stores and retrieves document content from Azure Blob Storage.
/// </summary>
public sealed class AzureBlobDocumentStorageService : IDocumentStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobDocumentStorageService" /> class.
    /// </summary>
    /// <param name="options">The bound document API options.</param>
    public AzureBlobDocumentStorageService(IOptions<DocumentApiOptions> options)
    {
        // <lab id="3">
        //|    // TODO Lab 3: Create the BlobServiceClient and target container client.
        var storageOptions = options.Value.Storage;
        var credential = new DefaultAzureCredential();
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
        // </lab>
    }

    /// <inheritdoc />
    public async Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken)
    {
        // <lab id="3">
        //|    throw new NotImplementedException("TODO Lab 3: Upload content to Blob Storage.");
        await EnsureInitializedAsync(cancellationToken);

        var blobClient = _containerClient.GetBlobClient(contentHash);

        var response = await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentHash = md5Hash,
                },
            },
            cancellationToken);

        await VerifyContentIntegrityAsync(blobClient, contentHash, md5Hash, response.Value.ContentHash, cancellationToken);
        // </lab>
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string contentHash, CancellationToken cancellationToken)
    {
        // <lab id="3">
        //|    throw new NotImplementedException("TODO Lab 3: Delete content from Blob Storage.");
        await EnsureInitializedAsync(cancellationToken);
        await _containerClient.DeleteBlobIfExistsAsync(contentHash, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        // </lab>
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken)
    {
        // <lab id="3">
        //|    throw new NotImplementedException("TODO Lab 3: Open content from Blob Storage.");
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            return await _containerClient.GetBlobClient(contentHash).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
        // </lab>
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
    /// Verifies that the MD5 hash reported by Azure Blob Storage matches the locally computed hash and removes
    /// the blob when a mismatch indicates the content was corrupted in transit.
    /// </summary>
    private static async Task VerifyContentIntegrityAsync(
        BlobClient blobClient,
        string contentHash,
        byte[] expectedHash,
        byte[]? storedHash,
        CancellationToken cancellationToken)
    {
        // <lab id="5">
        //|    // TODO Lab 5: Compare the returned content hash and delete corrupted blobs.
        //|    return;
        if (storedHash is not null && CryptographicOperations.FixedTimeEquals(expectedHash, storedHash))
        {
            return;
        }

        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        throw new DocumentStorageIntegrityException(contentHash, expectedHash, storedHash);
        // </lab>
    }
}