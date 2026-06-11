# Lab 5 - Upload Robustness

The upload happy path works, but real APIs need to be defensive. In this lab, you will reject invalid requests, detect duplicate content, clean up after dependency failures, and return predictable error responses.

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

## Understand The Resilience Pipeline

This lab introduces Polly, a .NET resilience library used to make dependency calls more reliable. Instead of writing retry loops by hand around every SQL Server call, the API centralizes the retry policy in `DocumentResiliencePipeline` and injects the resulting `ResiliencePipeline` into `DocumentService`.

The pipeline used in this workshop retries only failures that are usually temporary: transient `SqlException` values, `DbUpdateException` values caused by a transient SQL exception, and `TimeoutException`. It does not retry business errors such as duplicate documents, validation failures, or unsupported file types, because repeating those requests would not make them succeed.

The retry strategy uses exponential backoff with jitter. Exponential backoff waits longer after each failed attempt, while jitter adds a small random variation so many clients do not retry at exactly the same moment. This is a common pattern when a database is throttled, busy, restarting, or briefly unreachable.

In the service code, `_resiliencePipeline.ExecuteAsync(...)` means: run this database operation through the shared retry policy, pass the cancellation token through, and either return the result or rethrow the final exception if all retry attempts fail.

## Strengthen Upload Validation

Validation is deliberately outside the endpoint body. That keeps HTTP parsing separate from business rules and makes the rules easier to test in isolation later.

Open `DocumentUploadValidator.cs` and implement the upload rules inside the `Validate` method:

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

```csharp
return null;
```

This keeps the calling code straightforward: a validation failure contains a `ProblemDetails` response, and `null` means the request can continue.

As you can see, the validation logic is entirely separate from the endpoint and service. That makes it easier to test and maintain as the rules evolve.

## Detect Duplicates In The Service

The content hash is a stable fingerprint of the file bytes. If two uploads have the same hash, the API can treat them as the same document content even if the file name is different.

Open `DocumentService.cs`. In the `UploadAsync` method, just after computing the content hash, check for an existing document with the same hash:

```csharp
// ... command.Content.Position = 0;

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

The duplicate lookup is a good candidate for the pipeline because it depends on SQL Server and can fail transiently. Keeping the retry wrapper around the database call also keeps the rest of the method focused on document behavior instead of infrastructure retry mechanics.

Wrap the storage and database writes in a `try` block and track whether the blob was uploaded. If the blob upload succeeds but SQL persistence fails, the service can remove the blob so the two dependencies do not drift apart.

```csharp
var documentId = Guid.NewGuid().ToString("N");
var blobUploaded = false;

try
{
	await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);
	blobUploaded = true;

	// Previous code to create the entity, add it to the DbContext, then save with the resilience pipeline.
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

That sketch shows the shape of the defensive code, but the final method needs a little more care:

- Save SQL changes through `_resiliencePipeline` so transient database failures can be retried.
- Log storage integrity and Azure Storage dependency failures before rethrowing.
- Recheck for a conflicting document after `DbUpdateException`, because another request may have inserted the same content hash first.
- Only delete the blob when SQL failed and no duplicate row exists.
- Convert the duplicate race into `DuplicateDocumentException` so the endpoint can return `409 Conflict`.

At the end of this section, your `UploadAsync` method should look like this:

```csharp
public async Task<DocumentDto> UploadAsync(DocumentUploadCommand command, CancellationToken cancellationToken)
{
	if (!command.Content.CanSeek)
	{
		throw new ArgumentException("The upload content stream must support seeking.", nameof(command));
	}

	var stopwatch = Stopwatch.StartNew();
	var md5 = command.Content.ComputeMd5();
	var hash = Convert.ToHexString(md5);
	command.Content.Position = 0;

	// Polly retries transient SQL failures before the upload is allowed to continue.
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

	var documentId = Guid.NewGuid().ToString("N");
	var blobUploaded = false;

	try
	{
		// Store the blob first so the SQL row never points to content that was not saved.
		await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);
		blobUploaded = true;

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
		// Save metadata through Polly because SQL persistence can fail transiently.
		await _resiliencePipeline.ExecuteAsync(
			async token => await _dbContext.SaveChangesAsync(token),
			cancellationToken);

		var documentDto = ToDocumentDto(document);
		stopwatch.Stop();
		_activityMonitor.TrackUploadSucceeded(documentDto, stopwatch.Elapsed.TotalMilliseconds);
		return documentDto;
	}
	catch (DocumentStorageIntegrityException exception)
	{
		stopwatch.Stop();
		_logger.LogError(
			exception,
			"Document upload failed due to storage integrity validation. ContentHash={ContentHash} FileName={FileName} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
			hash,
			command.FileName,
			command.ContentType,
			command.Length,
			stopwatch.Elapsed.TotalMilliseconds);
		throw;
	}
	catch (RequestFailedException exception)
	{
		stopwatch.Stop();
		_logger.LogError(
			exception,
			"Document upload failed due to storage dependency error. ContentHash={ContentHash} FileName={FileName} StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode} DurationMs={DurationMs}",
			hash,
			command.FileName,
			exception.Status,
			exception.ErrorCode,
			stopwatch.Elapsed.TotalMilliseconds);
		throw;
	}
	catch (DbUpdateException exception)
	{
		// If SQL failed because another request inserted the same hash, report a duplicate instead of deleting the shared blob.
		var conflictingDocument = await _resiliencePipeline.ExecuteAsync(
			async token => await _dbContext.Documents
				.AsNoTracking()
				.FirstOrDefaultAsync(document => document.ContentHash == hash, token),
			cancellationToken);

		if (conflictingDocument is null)
		{
			if (blobUploaded)
			{
				try
				{
					// Roll back only the blob created by this attempt when there is no duplicate owner.
					await _storage.DeleteAsync(hash, cancellationToken);
				}
				catch (Exception cleanupException) when (cleanupException is not OperationCanceledException)
				{
					_logger.LogWarning(
						cleanupException,
						"Document upload cleanup failed while deleting blob after a database error. ContentHash={ContentHash}",
						hash);
				}
			}

			stopwatch.Stop();
			_logger.LogError(
				exception,
				"Document upload failed due to a database error without duplicate match. ContentHash={ContentHash} FileName={FileName} DurationMs={DurationMs}",
				hash,
				command.FileName,
				stopwatch.Elapsed.TotalMilliseconds);
			throw;
		}

		stopwatch.Stop();
		_activityMonitor.TrackUploadDuplicate(
			conflictingDocument.Id,
			command.ContentType,
			command.Length,
			stopwatch.Elapsed.TotalMilliseconds);
		throw new DuplicateDocumentException(conflictingDocument.Id);
	}
	catch (Exception exception) when (exception is not OperationCanceledException)
	{
		stopwatch.Stop();
		_logger.LogError(
			exception,
			"Document upload failed unexpectedly. ContentHash={ContentHash} FileName={FileName} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
			hash,
			command.FileName,
			command.ContentType,
			command.Length,
			stopwatch.Elapsed.TotalMilliseconds);
		throw;
	}
}
```

<div class="tip" data-title="Duplicate races">

> The final solution rechecks for a conflicting content hash when SQL fails. That avoids deleting a blob that belongs to another request that uploaded the same content at the same time.

</div>

## Map Errors At The Endpoint

The service throws domain or dependency exceptions. The endpoint translates those exceptions into HTTP responses that clients can understand and handle consistently.

Open `DocumentEndpoints.cs` and wrap the service call:

```csharp
try
{
	var document = await documentService.UploadAsync(
		new DocumentUploadCommand(file.FileName, file.ContentType, fileStream, file.Length, metadataResult.Metadata!),
		cancellationToken);

	return Results.Json(document, statusCode: StatusCodes.Status201Created);
}
catch (DuplicateDocumentException exception)
{
	logger.LogWarning(exception, "Duplicate document upload rejected.");
	return Results.Problem(
		detail: "A document with the same content already exists.",
		statusCode: StatusCodes.Status409Conflict);
}
catch (DocumentStorageIntegrityException exception)
{
	logger.LogError(exception, "Document upload failed because storage integrity verification failed.");
	return Results.Problem(
		detail: "The document storage service reported a content integrity failure.",
		statusCode: StatusCodes.Status502BadGateway);
}
catch (RequestFailedException exception)
{
	logger.LogError(
		exception,
		"Document upload failed because the storage dependency is unavailable. StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode}",
		exception.Status,
		exception.ErrorCode);
	return Results.Problem(
		detail: "The document storage service is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (DbUpdateException exception)
{
	logger.LogError(exception, "Document upload failed because the database dependency is unavailable.");
	return Results.Problem(
		detail: "The document database is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (TimeoutException exception)
{
	logger.LogError(exception, "Document upload failed due to a dependency timeout.");
	return Results.Problem(
		detail: "A downstream dependency timed out while processing the request.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
	logger.LogError(exception, "Document upload failed due to an unexpected error.");
	return Results.Problem(
		detail: "An unexpected error occurred while processing the document request.",
		statusCode: StatusCodes.Status500InternalServerError);
}
```

Logging belongs at the boundary where the API translates the exception into HTTP; the service also logs the lower-level details it owns, such as the content hash and storage status code.

The important idea is consistency. Clients should not need to know whether the failure came from SQL Server, Blob Storage, or the document workflow internals.

## Run And Test The Upload

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http` and send the upload request again. The first valid upload should still return `201 Created`.

Then send the same upload a second time to validate duplicate detection.

<div class="task" data-title="Validation">

> Try these scenarios from `src/http/requests.http`:
>
> - unsupported content type returns `400 Bad Request`
> - duplicate content returns `409 Conflict`

</div>

---