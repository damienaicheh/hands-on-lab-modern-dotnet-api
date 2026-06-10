# Lab 4 - Upload Happy Path

You now have metadata persistence and blob storage. In this lab, you will connect them through the first real document workflow: upload a valid multipart request, save the file, persist metadata, and return `201 Created`.

This lab focuses on the happy path. Robust validation and dependency failure handling come next.

The goal is to see the full route from HTTP request to database row and blob content. Once that path exists, it becomes much easier to harden it.

## What You Will Learn

In this lab, you will:

- Read a multipart form request from a Minimal API endpoint.
- Deserialize the metadata JSON part.
- Call the document service from the endpoint.
- Save file content to Blob Storage.
- Save document metadata to SQL Server.
- Return a `DocumentDto` response.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

Contracts, DTOs, storage, and database services are already provided.

## Implement The Upload Endpoint

Open `DocumentEndpoints.cs` and find the `UploadAsync` handler.

The endpoint should stay thin: it understands HTTP, form data, and response codes. The service will own the actual document workflow.

Read the form data:

```csharp
var logger = loggerFactory.CreateLogger("DocumentEndpoints");

if (!request.HasFormContentType)
{
	return Results.Problem(
		detail: "The request must use multipart/form-data.",
		statusCode: StatusCodes.Status400BadRequest);
}

var form = await request.ReadFormAsync(cancellationToken);
var file = form.Files.GetFile("file");
var metadataResult = TryReadMetadata(form["metadata"]);

if (metadataResult.Error is not null)
{
	return Results.Problem(metadataResult.Error);
}
```

Then call the service:

The endpoint passes a command object to the service instead of many separate parameters. That makes the upload intent explicit and keeps the method signature readable.

```csharp
var validationFailure = validator.Validate(file, metadataResult.Metadata);

if (validationFailure is not null)
{
	return Results.Problem(validationFailure.Problem);
}

await using var fileStream = file!.OpenReadStream();

var document = await documentService.UploadAsync(
	new DocumentUploadCommand(file.FileName, file.ContentType, fileStream, file.Length, metadataResult.Metadata!),
	cancellationToken);

return Results.Json(document, statusCode: StatusCodes.Status201Created);
```

As you can see, the endpoint also handles validation failures by returning problem details. The `Results.Problem` method is a convenient way to create a problem details response with the appropriate content type and status code. You don't need to create a`APIError` class or `APIResponse` class manually it's all handled by the `Results` class provided by ASP.NET Core.

## Implement The Service Happy Path

Open `DocumentService.cs` and implement `UploadAsync`.

This is where the application switches from HTTP concerns to business concerns: compute an identity for the content, store the bytes, store the metadata, and return the public DTO.

Compute the content hash and reset the stream:

```csharp
if (!command.Content.CanSeek)
{
	throw new ArgumentException("The upload content stream must support seeking.", nameof(command));
}

var stopwatch = Stopwatch.StartNew();
var md5 = command.Content.ComputeMd5();
var hash = Convert.ToHexString(md5);
command.Content.Position = 0;
```

Save the blob and persist metadata:

```csharp
var documentId = Guid.NewGuid().ToString("N");

await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);

var (title, description, source, tags) = NormalizeMetadata(command.Metadata);
var document = new Document
{
	Id = documentId,
	FileName = command.FileName,
	ContentType = command.ContentType,
	Size = command.Length,
	Title = title,
	Description = description,
	Source = source,
	Tags = tags,
	ContentHash = hash,
	CreatedUtc = DateTimeOffset.UtcNow,
};

_dbContext.Documents.Add(document);
await _dbContext.SaveChangesAsync(cancellationToken);

var documentDto = ToDocumentDto(document);
stopwatch.Stop();
_activityMonitor.TrackUploadSucceeded(documentDto, stopwatch.Elapsed.TotalMilliseconds);
return documentDto;
```

The `NormalizeMetadata` helper trims text fields, removes empty tags, and keeps only one copy of each tag using a case-insensitive comparison. That way the API stores clean metadata from the first upload workflow.

As you can see, the storage service is responsible for saving the file content, while the database context is responsible for saving the metadata. 

A stopwatch is used to track the time taken for the upload operation, and the activity monitor is used to log a successful custom upload event with the document details and elapsed time. While time taken by an endpoint can be automatically tracked by Application Insights, custom events like this upload succeeded can provide more granular insights into specific operations within your application. You will see the implementation of the `TrackUploadSucceeded` method in the next lab when you implement Application Insights integration.

Then the service maps the `Document` entity which represent the database object to a `DocumentDto` that can be returned to the client. Let's implement it:

```csharp
private static DocumentDto ToDocumentDto(Document document)
{
	return new DocumentDto
	{
		Id = document.Id,
		FileName = document.FileName,
		ContentType = document.ContentType,
		Size = document.Size,
		Metadata = new DocumentMetadataDto
		{
			Title = document.Title,
			Description = document.Description,
			Source = document.Source,
			Tags = document.Tags.Count > 0 ? document.Tags : null,
		},
	};
}
```

By doing so, you ensure that the API response is decoupled from the internal database representation, allowing for more flexibility in how you manage and evolve your data models over time.

## Register The Document Service

Open `DependencyInjection.cs` and update the service registration to include the `DocumentService` implementation:

```csharp
services.AddScoped<IDocumentService, DocumentService>();
```

## Test The Upload

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Inside the Solution Items, open the `http/requests.http` file to send a multipart upload:

![Send request to upload](./assets/send-upload-request-happy-path.png)

You must see the `201 Created` response with the uploaded document details and if you go inside your Azure SQL Database you should see the new document metadata row created:

![Document metadata in Azure SQL Database](./assets/document-metadata-happy-path.png)

If you check your resource group, inside the Storage Account, inside **Containers** select the container named **documents** and you should see the new blob with the content of the uploaded file:

![Blob content in Azure Storage](./assets/blob-content-happy-path.png)

To upload new files you can modify the name `sample-1.pdf` to point to other files in the `files` folder.

<div class="task" data-title="Validation">

> A valid upload should return `201 Created` with a `DocumentDto` payload.
>
> Duplicate detection and advanced error handling are not implemented yet. That is the purpose of the next lab.

</div>

---