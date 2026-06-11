# Lab 6 - Download and Search Functionality

The API can now upload documents. In this lab, you will let clients retrieve stored content and search document metadata.

Search and download complete the core document workflow.

After this lab, a document can go through the complete lifecycle from upload to retrieval. The API becomes useful enough to validate with real end-to-end scenarios.

## What You Will Learn

In this lab, you will:

- Query document metadata by id.
- Open document content from Blob Storage.
- Return `404 Not Found` when metadata or content is missing.
- Search documents with optional filters.
- Expose download and search endpoints.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

The search criteria and download result contracts are already provided.

## Implement Download In The Service

Download needs both dependencies: SQL tells you which blob to read and what content type to return, while Blob Storage provides the actual stream.

Open `DocumentService.cs` and implement `DownloadAsync`:

```csharp
var stopwatch = Stopwatch.StartNew();

// Polly passes this token into the delegate so Entity Framework Core can cancel the SQL query if the request is aborted.
// AsNoTracking method keeps this read-only metadata lookup lighter because the entity will not be updated.
var document = await _resiliencePipeline.ExecuteAsync(
	async token => await _dbContext.Documents
		.AsNoTracking()
		.FirstOrDefaultAsync(storedDocument => storedDocument.Id == id, token),
	cancellationToken);

if (document is null)
{
	_activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
	return null;
}

var stream = await _storage.OpenReadAsync(document.ContentHash, cancellationToken);

if (stream is null)
{
	_activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
	return null;
}

_activityMonitor.TrackDownloadSucceeded(document.Id, document.ContentType, document.Size, stopwatch.Elapsed.TotalMilliseconds);
return new DocumentContentResult(document.FileName, document.ContentType, stream);
```

This uses the same Polly pipeline introduced in the upload robustness lab. Only the SQL metadata lookup is wrapped because that is the dependency call covered by this retry policy; the Blob Storage read is handled separately by the storage service and the Azure SDK retry configuration.

Inside the lambda, `token` is the cancellation token Polly gives to the operation. It comes from the `cancellationToken` passed to `ExecuteAsync`, and Entity Framework Core receives it so the SQL query can stop if the HTTP request is cancelled. The `storedDocument` parameter represents each document row Entity Framework Core checks while building the `WHERE` clause for `Id == id`; it is just a local name, chosen to make the predicate easier to read.

`AsNoTracking()` tells Entity Framework Core that this query is read-only. The service only needs metadata to locate the blob and return a response, so Entity Framework Core does not need to keep change-tracking information for the entity.

Keep the same structure as upload: start the stopwatch, wrap the dependency calls in `try`, log dependency failures, and let the endpoint translate them into HTTP responses.

```csharp
var stopwatch = Stopwatch.StartNew();

try
{
	// Code you have done previously: Query metadata, open the blob stream, track success or not found, then return the result.
}
catch (DocumentStorageIntegrityException exception)
{
	stopwatch.Stop();
	_logger.LogError(
		exception,
		"Document download failed due to storage integrity validation. DocumentId={DocumentId} DurationMs={DurationMs}",
		id,
		stopwatch.Elapsed.TotalMilliseconds);
	throw;
}
catch (RequestFailedException exception)
{
	stopwatch.Stop();
	_logger.LogError(
		exception,
		"Document download failed due to storage dependency error. DocumentId={DocumentId} StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode} DurationMs={DurationMs}",
		id,
		exception.Status,
		exception.ErrorCode,
		stopwatch.Elapsed.TotalMilliseconds);
	throw;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
	stopwatch.Stop();
	_logger.LogError(
		exception,
		"Document download failed unexpectedly. DocumentId={DocumentId} DurationMs={DurationMs}",
		id,
		stopwatch.Elapsed.TotalMilliseconds);
	throw;
}
```

## Implement Search In The Service

The query starts with filters that SQL Server can handle efficiently. After loading the narrowed set, the service applies tag and free-text checks in memory to keep the lab code approachable.

Implement the metadata query in `QueryDocumentsAsync`:

```csharp
var query = _dbContext.Documents.AsNoTracking();

if (!string.IsNullOrWhiteSpace(criteria.Title))
{
	query = query.Where(document => document.Title == criteria.Title);
}

if (!string.IsNullOrWhiteSpace(criteria.ContentType))
{
	query = query.Where(document => document.ContentType == criteria.ContentType);
}

var documents = await query
	.OrderByDescending(document => document.CreatedUtc)
	.ToListAsync(cancellationToken);
```

Then apply filters that are easier to evaluate in memory:

```csharp
IEnumerable<Document> filtered = documents;

if (!string.IsNullOrWhiteSpace(criteria.Tag))
{
	filtered = filtered.Where(document =>
		document.Tags.Any(tag => string.Equals(tag, criteria.Tag, StringComparison.OrdinalIgnoreCase)));
}

if (!string.IsNullOrWhiteSpace(criteria.Query))
{
	filtered = filtered.Where(document => ContainsFreeText(document, criteria.Query));
}

return filtered.Select(ToDocumentDto).ToArray();
```

Then update `SearchAsync` so the query is executed through the resilience pipeline, tracked by the activity monitor, and logged when a dependency fails:

```csharp
var stopwatch = Stopwatch.StartNew();

try
{
	var documents = await _resiliencePipeline.ExecuteAsync(
		async token => await QueryDocumentsAsync(criteria, token),
		cancellationToken);

	_activityMonitor.TrackSearch(criteria, documents.Count, cacheHit: false);
	return documents;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
	stopwatch.Stop();
	_logger.LogError(
		exception,
		"Document search failed. DurationMs={DurationMs} HasQuery={HasQuery} HasTitleFilter={HasTitleFilter} HasTagFilter={HasTagFilter} HasContentTypeFilter={HasContentTypeFilter}",
		stopwatch.Elapsed.TotalMilliseconds,
		!string.IsNullOrWhiteSpace(criteria.Query),
		!string.IsNullOrWhiteSpace(criteria.Title),
		!string.IsNullOrWhiteSpace(criteria.Tag),
		!string.IsNullOrWhiteSpace(criteria.ContentType));
	throw;
}
```

Here again, Polly protects the SQL query, not the full endpoint. If the query keeps failing after the configured retry attempts, the exception still flows to the `catch` block so the service can log it and the endpoint can return a predictable error response.

The `cacheHit` value is always `false` in this lab. The next lab will add the cache and replace this direct query with cache-aware behavior.

## Expose The Endpoints

Open `DocumentEndpoints.cs` and implement the search handler:

The endpoint only builds a `DocumentSearchCriteria` object from query string values. This keeps filtering rules inside the service instead of spreading them through the HTTP layer.

```csharp
var documents = await documentService.SearchAsync(
	new DocumentSearchCriteria(query, title, tag, contentType),
	cancellationToken);

return Results.Ok(documents);
```

Wrap the service call so dependency failures become predictable API responses:

```csharp
var logger = loggerFactory.CreateLogger("DocumentEndpoints");

try
{
	var documents = await documentService.SearchAsync(
		new DocumentSearchCriteria(query, title, tag, contentType),
		cancellationToken);

	return Results.Ok(documents);
}
catch (DbUpdateException exception)
{
	logger.LogError(exception, "Document search failed because the database dependency is unavailable.");
	return Results.Problem(
		detail: "The document database is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (TimeoutException exception)
{
	logger.LogError(exception, "Document search failed due to a dependency timeout.");
	return Results.Problem(
		detail: "A downstream dependency timed out while processing the request.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
	logger.LogError(exception, "Document search failed due to an unexpected error.");
	return Results.Problem(
		detail: "An unexpected error occurred while processing the document request.",
		statusCode: StatusCodes.Status500InternalServerError);
}
```

Then implement download:

`Results.File` streams the content back to the caller and keeps the original file name and content type. Range processing is enabled so clients can resume or partially read supported downloads.

```csharp
var document = await documentService.DownloadAsync(id, cancellationToken);

if (document is null)
{
	return Results.Problem(
		detail: "The requested document was not found.",
		statusCode: StatusCodes.Status404NotFound);
}

return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
```

Use the same boundary pattern for download errors:

```csharp
var logger = loggerFactory.CreateLogger("DocumentEndpoints");

try
{
	var document = await documentService.DownloadAsync(id, cancellationToken);

	if (document is null)
	{
		return Results.Problem(
			detail: "The requested document was not found.",
			statusCode: StatusCodes.Status404NotFound);
	}

	return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
}
catch (RequestFailedException exception)
{
	logger.LogError(
		exception,
		"Document download failed because the storage dependency is unavailable. StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode}",
		exception.Status,
		exception.ErrorCode);
	return Results.Problem(
		detail: "The document storage service is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (DbUpdateException exception)
{
	logger.LogError(exception, "Document download failed because the database dependency is unavailable.");
	return Results.Problem(
		detail: "The document database is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (TimeoutException exception)
{
	logger.LogError(exception, "Document download failed due to a dependency timeout.");
	return Results.Problem(
		detail: "A downstream dependency timed out while processing the request.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
	logger.LogError(exception, "Document download failed due to an unexpected error.");
	return Results.Problem(
		detail: "An unexpected error occurred while processing the document request.",
		statusCode: StatusCodes.Status500InternalServerError);
}
```

## Run And Test The Workflow

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http` and run the document workflow requests in order:

1. Upload a document.
2. Search documents with the `Search documents` request.
3. Download the uploaded document with the `Download the last uploaded document` request.

The download request uses the id returned by the upload request, so send the upload request first.

<div class="task" data-title="Validation">

> Upload a document, search for it, then download its content from `src/http/requests.http`.
>
> Also try downloading an unknown id and confirm that the API returns `404 Not Found`.

</div>

---