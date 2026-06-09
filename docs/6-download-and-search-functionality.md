# Lab 6 - Download and Search Functionality

The API can now upload documents reliably. In this lab, you will let clients retrieve stored content and search document metadata.

Search and download complete the core document workflow.

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

Open `DocumentService.cs` and implement `DownloadAsync`:

Download needs both dependencies: SQL tells you which blob to read and what content type to return, while Blob Storage provides the actual stream.

```csharp
var document = await _resiliencePipeline.ExecuteAsync(
	async token => await _dbContext.Documents
		.AsNoTracking()
		.FirstOrDefaultAsync(candidate => candidate.Id == id, token),
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

## Implement Search In The Service

Implement the metadata query in `QueryDocumentsAsync`:

The query starts with filters that SQL Server can handle efficiently. After loading the narrowed set, the service applies tag and free-text checks in memory to keep the lab code approachable.

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

## Expose The Endpoints

Open `DocumentEndpoints.cs` and implement the search handler:

The endpoint only builds a `DocumentSearchCriteria` object from query string values. This keeps filtering rules inside the service instead of spreading them through the HTTP layer.

```csharp
var documents = await documentService.SearchAsync(
	new DocumentSearchCriteria(query, title, tag, contentType),
	cancellationToken);

return Results.Ok(documents);
```

Then implement download:

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

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Upload a document, search for it, then download its content.
>
> Also try downloading an unknown id and confirm that the API returns `404 Not Found`.

</div>

---