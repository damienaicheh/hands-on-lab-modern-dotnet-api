# Lab 3 - Blob Storage Integration

In the previous lab, you wired SQL Server to store the file metadata of your documents. Now you will add Azure Blob Storage for the binary content of uploaded documents.

The API keeps metadata and content separated: SQL Server stores searchable properties, while Blob Storage stores the file bytes.

This separation is common in document systems: the database is great for filters and relationships, while storage accounts are built for durable file content.

## What You Will Learn

In this lab, you will:

- Instantiate a `BlobServiceClient` from the Azure SDK with `DefaultAzureCredential`.
- Resolve the configured blob container.
- Implement the storage service methods to:
	- Save content to Blob Storage.
	- Open document content for download.
	- Delete content when cleanup is required.
- Register the storage service in dependency injection.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Storage/AzureBlobDocumentStorageService.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

The storage interface, options, packages, and configuration keys are already provided.

## Create The Blob Client

Open `AzureBlobDocumentStorageService.cs` and implement the constructor:

Blob Storage is the right place for the binary file content because it is optimized for streams and large objects. SQL Server stays focused on metadata that you need to query. The file uploaded will be stored as a blob.

```csharp
public AzureBlobDocumentStorageService(IOptions<DocumentApiOptions> options)
{
	var storageOptions = options.Value.Storage;
	var credential = new DefaultAzureCredential();
	var blobServiceClient = new BlobServiceClient(new Uri(storageOptions.ServiceUri), credential);
	_containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
}
```

As you can see, the Azure SDK client is straightforward to instantiate. The `ServiceUri` and `ContainerName` come from configuration, and the credential uses `DefaultAzureCredential`, which is a great option for local development and Azure-hosted environments. Locally, it can use your Azure CLI sign-in, and in Azure, it can use a managed identity if configured.

## Save Content

The storage service receives a stream, not a byte array. This keeps the API friendly to larger files because callers do not need to load everything into memory before saving.

Implement `SaveAsync`:

```csharp
public async Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);

	var blobClient = _containerClient.GetBlobClient(contentHash);
	await blobClient.UploadAsync(content, cancellationToken);
}
```

The blob name uses the content hash. This makes duplicate content easier to detect in later labs. As you can see the Azure SDK makes it easy to upload a stream with `UploadAsync`.

## Open And Delete Content

Deletion is used later when the upload workflow needs to roll back a blob after a database failure. Keeping it in the storage abstraction makes the business service easier to read.

Implement `DeleteAsync`:

```csharp
public async Task DeleteAsync(string contentHash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);
	await _containerClient.DeleteBlobIfExistsAsync(
		contentHash,
		DeleteSnapshotsOption.IncludeSnapshots,
		cancellationToken: cancellationToken);
}
```

Then implement `OpenReadAsync`:

Returning `null` for a missing blob keeps the service contract simple. The document service can then decide whether that becomes a `404` or another recovery path.

```csharp
public async Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);

	try
	{
		return await _containerClient.GetBlobClient(contentHash).OpenReadAsync(cancellationToken: cancellationToken);
	}
	catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
	{
		return null;
	}
}
```

## Register The Storage Service

Open `DependencyInjection.cs` and after the `DocumentDbContext` registration, register the Azure Service implementation:

```csharp
services.AddSingleton<IDocumentStorageService, AzureBlobDocumentStorageService>();
```

<div class="tip" data-title="Why singleton?">

> Azure SDK clients are thread-safe and designed to be reused. A singleton avoids recreating clients for every request.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> The project should build successfully.
>
> Upload is still incomplete, but the API now has a storage implementation ready for the next lab.

</div>

---