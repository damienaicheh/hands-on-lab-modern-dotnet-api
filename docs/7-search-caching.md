# Lab 7 - Search Caching

Search is often called repeatedly with the same filters. In this lab, you will add in-memory caching to reduce repeated database work while keeping the API contract unchanged.

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

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`

The cache options and shared cache version service are already provided.

## Register Memory Cache

Open `Program.cs` and register the memory cache:

```csharp
builder.Services.AddMemoryCache();
```

## Add Cache Around Search

Open `DocumentService.cs` and update `SearchAsync`.

Create the key and check the cache:

```csharp
var cacheKey = CreateCacheKey(_cacheVersion.Current, criteria);

var cacheHit = _cache.TryGetValue(cacheKey, out IReadOnlyList<DocumentDto>? cachedDocuments)
	&& cachedDocuments is not null;
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
```

## Create A Deterministic Cache Key

Add the helper methods:

```csharp
private static string CreateCacheKey(int cacheVersion, DocumentSearchCriteria criteria)
{
	return string.Join(
		"::",
		"documents-search",
		cacheVersion,
		NormalizeCacheSegment(criteria.Query),
		NormalizeCacheSegment(criteria.Title),
		NormalizeCacheSegment(criteria.Tag),
		NormalizeCacheSegment(criteria.ContentType));
}

private static string NormalizeCacheSegment(string? value)
{
	return string.IsNullOrWhiteSpace(value)
		? string.Empty
		: value.Trim().ToLowerInvariant();
}
```

The shared cache version is part of the key. Incrementing it invalidates all previous search entries without having to enumerate cache keys.

## Invalidate After Upload

After a successful upload and database save, increment the cache version:

```csharp
_cacheVersion.Increment();
```

<div class="tip" data-title="Why not remove cache entries one by one?">

> Search has many possible filter combinations. A versioned key is simpler and avoids tracking every possible cache key manually.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Run the same search twice and confirm that the second call uses the cached path.
>
> Upload a new document, search again, and confirm the cache is invalidated.

</div>

---