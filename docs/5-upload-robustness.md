# Lab 5 - Upload Robustness

The upload happy path works, but real APIs need to be defensive. In this lab, you will reject invalid requests, detect duplicate content, clean up after dependency failures, and return predictable error responses.

This is the lab where the upload workflow becomes production-shaped.

You will keep the successful path from the previous lab, then add the defensive behavior around it: reject bad input early, avoid duplicate content, and clean up when one dependency succeeds but another fails.

## What You Will Learn

In this lab, you will:

- Validate multipart uploads.
- Reject empty files and unsupported content types.
- Detect duplicate documents using a content hash.
- Return `409 Conflict` for duplicate content.
- Clean up Blob Storage when SQL persistence fails.
- Use a resilience pipeline around database operations.
- Map dependency errors to clean HTTP responses.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Validators/Documents/DocumentUploadValidator.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Exceptions, upload options, content type helpers, and the resilience pipeline are already provided.

## Strengthen Upload Validation

Open `DocumentUploadValidator.cs` and implement the upload rules:

Validation is deliberately outside the endpoint body. That keeps HTTP parsing separate from business rules and makes the rules easier to test in isolation later.

```csharp
if (file is null)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The file part is required.",
	});
}

if (metadata is null)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The metadata part is required.",
	});
}
```

Then add size and content type checks:

```csharp
if (file.Length <= 0)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The uploaded file cannot be empty.",
	});
}

if (file.Length > _options.Upload.MaxFileSizeBytes)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status413PayloadTooLarge,
		Title = "Payload Too Large",
		Detail = $"The uploaded file exceeds the configured maximum size of {_options.Upload.MaxFileSizeBytes} bytes.",
	});
}

if (!DocumentContentTypes.IsSupported(file.ContentType))
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "Unsupported content type. Allowed values are PDF, TXT, DOC, and DOCX.",
	});
}
```

Return `null` when the request is valid.

This keeps the calling code straightforward: a validation failure contains a `ProblemDetails` response, and `null` means the request can continue.

## Detect Duplicates In The Service

Open `DocumentService.cs`. Before saving to storage, check whether a document with the same content hash already exists:

The content hash is a stable fingerprint of the file bytes. If two uploads have the same hash, the API can treat them as the same document content even if the file name is different.

```csharp
var existingDocument = await _resiliencePipeline.ExecuteAsync(
	async token => await _dbContext.Documents
		.AsNoTracking()
		.FirstOrDefaultAsync(document => document.ContentHash == hash, token),
	cancellationToken);

if (existingDocument is not null)
{
	stopwatch.Stop();
	_activityMonitor.TrackUploadDuplicate(
		existingDocument.Id,
		command.ContentType,
		command.Length,
		stopwatch.Elapsed.TotalMilliseconds);
	throw new DuplicateDocumentException(existingDocument.Id);
}
```

Wrap the storage and database writes in a `try` block and track whether the blob was uploaded:

```csharp
var documentId = Guid.NewGuid().ToString("N");
var blobUploaded = false;

try
{
	await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);
	blobUploaded = true;

	// Create the entity, add it to the DbContext, then save with the resilience pipeline.
}
catch (DbUpdateException exception)
{
	if (blobUploaded)
	{
		await _storage.DeleteAsync(hash, cancellationToken);
	}

	_logger.LogError(exception, "Document upload failed due to a database error. ContentHash={ContentHash}", hash);
	throw;
}
```

<div class="tip" data-title="Duplicate races">

> The final solution rechecks for a conflicting content hash when SQL fails. That avoids deleting a blob that belongs to another request that uploaded the same content at the same time.

</div>

## Map Errors At The Endpoint

Open `DocumentEndpoints.cs` and wrap the service call:

The service throws domain or dependency exceptions. The endpoint translates those exceptions into HTTP responses that clients can understand and handle consistently.

```csharp
try
{
	var document = await documentService.UploadAsync(
		new DocumentUploadCommand(file.FileName, file.ContentType, fileStream, file.Length, metadataResult.Metadata!),
		cancellationToken);

	return Results.Json(document, statusCode: StatusCodes.Status201Created);
}
catch (DuplicateDocumentException)
{
	return Results.Problem(
		detail: "A document with the same content already exists.",
		statusCode: StatusCodes.Status409Conflict);
}
catch (DbUpdateException)
{
	return Results.Problem(
		detail: "The document database is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
```

Add storage and unexpected error mappings using the same pattern.

The important idea is consistency. Clients should not need to know whether the failure came from SQL Server, Blob Storage, or the document workflow internals.

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Try these scenarios from Swagger UI or the HTTP file:
>
> - missing metadata returns `400 Bad Request`
> - empty file returns `400 Bad Request`
> - unsupported content type returns `400 Bad Request`
> - duplicate content returns `409 Conflict`

</div>

---