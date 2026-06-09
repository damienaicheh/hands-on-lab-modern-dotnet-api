# Lab 4 - Upload Happy Path

You now have metadata persistence and blob storage. In this lab, you will connect them through the first real document workflow: upload a valid multipart request, save the file, persist metadata, and return `201 Created`.

This lab focuses on the happy path. Robust validation and dependency failure handling come next.

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

Contracts, DTOs, storage, and database services are already provided.

## Implement The Upload Endpoint

Open `DocumentEndpoints.cs` and find the `UploadAsync` handler.

Read the form data:

```csharp
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

## Implement The Service Happy Path

Open `DocumentService.cs` and implement `UploadAsync`.

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

var document = new Document
{
	Id = documentId,
	FileName = command.FileName,
	ContentType = command.ContentType,
	Size = command.Length,
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

Then implement the DTO mapping helper:

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

## Test The Upload

Build the project:

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

You can use Swagger UI or `src/http/requests.http` to send a multipart upload.

<div class="task" data-title="Validation">

> A valid upload should return `201 Created` with a `DocumentDto` payload.
>
> Duplicate detection and advanced error handling are not implemented yet. That is the purpose of the next lab.

</div>

---