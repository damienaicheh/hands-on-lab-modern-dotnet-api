# Lab 3 - Blob Storage Integration

In the previous lab, you wired SQL Server for metadata. Now you will add Azure Blob Storage for the binary content of uploaded documents.

The API keeps metadata and content separated: SQL Server stores searchable properties, while Blob Storage stores the file bytes.

## What You Will Learn

In this lab, you will:

- Create a `BlobServiceClient` with `DefaultAzureCredential`.
- Resolve the configured blob container.
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

```csharp
public AzureBlobDocumentStorageService(IOptions<DocumentApiOptions> options)
{
	var storageOptions = options.Value.Storage;
	var credential = new DefaultAzureCredential();
	var blobServiceClient = new BlobServiceClient(new Uri(storageOptions.ServiceUri), credential);
	_containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
}
```

`DefaultAzureCredential` can use your Azure CLI sign-in locally and managed identity in Azure.

## Save Content

Implement `SaveAsync`:

```csharp
public async Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);

	var blobClient = _containerClient.GetBlobClient(contentHash);
	await blobClient.UploadAsync(content, cancellationToken);
}
```

The blob name uses the content hash. This makes duplicate content easier to detect in later labs.

## Open And Delete Content

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

Open `DependencyInjection.cs` and register the Azure implementation:

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