# Lab 7 - Search Caching

Search is often called repeatedly with the same filters. In this lab, you will add in-memory caching to reduce repeated database work while keeping the API contract unchanged. In a real world scenario, the cache can be handled by an API Gateway in front of the API, but this lab focuses on the caching behavior itself.

The important part is not just caching; it is caching safely and invalidating results when new documents are uploaded.

## What You Will Learn

In this lab, you will:

- Register `IMemoryCache`.
- Create a deterministic cache key from search criteria.
- Cache search results with a configurable TTL.
- Track cache hit and cache miss behavior.
- Invalidate search results after upload.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Documents/DocumentService.cs`

The cache options and shared cache version service are already provided.

## Add Cache Around Search

Caching belongs around the service query, not inside the endpoint. This way every caller benefits from the same behavior, even if another endpoint or background process reuses the service later. It's also easier to test the caching behavior in isolation.

Open `DocumentService.cs` and update the entire `SearchAsync` with the code below:

```csharp
var stopwatch = Stopwatch.StartNew();

try
{
	var cacheKey = CreateCacheKey(_cacheVersion.Current, criteria);

	var cacheHit = _cache.TryGetValue(cacheKey, out IReadOnlyList<DocumentDto>? cachedDocuments) && cachedDocuments is not null;
	IReadOnlyList<DocumentDto> documents;

	if (cacheHit)
	{
		documents = cachedDocuments!;
	}
	else
	{
		documents = await _resiliencePipeline.ExecuteAsync(
			async token => await QueryDocumentsAsync(criteria, token),
			cancellationToken);
		_cache.Set(
			cacheKey,
			documents,
			new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.Search.CacheTtlSeconds)),
			});
	}

	_activityMonitor.TrackSearch(criteria, documents.Count, cacheHit);

	return documents;
	// </lab>
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

As you can see the cache key is created from the search criteria and shared cache version. The service first checks for a cache hit and returns cached results if they exist. If not, it executes the query, stores the results in cache with a TTL, and returns them.

The shared cache version is part of the key as you can see in the `CreateCacheKey` method. Incrementing it invalidates all previous search entries without having to enumerate cache keys.

## Invalidate After Upload

After a successful upload and database save, increment the cache version inside the `UploadAsync` method:

```csharp
// await _resiliencePipeline.ExecuteAsync(...);

_cacheVersion.Increment();

// var documentDto = ToDocumentDto(document);
```

<div class="tip" data-title="Why not remove cache entries one by one?">

> Search has many possible filter combinations. A versioned key is simpler and avoids tracking every possible cache key manually.

</div>

## Run And Test Search Caching

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http`, upload a document, then send the `Search documents` request twice with the same query.

After that, upload another document and send the same search request again. The upload should invalidate previous search entries by incrementing the shared cache version.

<div class="task" data-title="Validation">

> Run the same search twice from `src/http/requests.http` and confirm that the second call uses the cached path.
>
> Upload a new document, search again, and confirm the cache is invalidated.
> You can check the time of the response to confirm caching behavior.

</div>

---