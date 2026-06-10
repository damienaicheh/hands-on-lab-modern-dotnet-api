# Lab 9 - Unit Testing

You now have the main API behaviors in place. In this lab, you will add automated tests so the upload, search, download, duplicate detection, and edge cases can be validated repeatedly.

Each test should explain one behavior in code: what is arranged, what action happens, and what result proves the behavior is correct.

This lab focuses on `DocumentService` because it contains most of the document workflow rules. Endpoint tests are valuable too, but service tests are faster, easier to debug, and precise enough to protect the business behavior you implemented in the previous labs.

## What You Will Learn

In this lab, you will:

- Test `DocumentService` with EF Core InMemory.
- Use a fake document storage service.
- Verify search cache behavior.
- Verify upload happy path behavior.
- Verify duplicate detection.
- Verify missing and existing downloads.
- Cover endpoint behavior through the test factory.

## Files To Open

You only need to edit these files:

- `tests/DocumentAPI.Tests/DocumentServiceTests.cs`

The fake storage, package references, helper methods, and internal visibility setup are already provided. You will fill in the tests that use them.

## Understand The Test Helpers

The tests rely on a few helper types in the same file. You do not need to rewrite them, but it helps to understand why they exist.

`CreateDbContext` creates a fresh Entity Framework Core InMemory database for every test. The unique database name keeps tests isolated, so one test cannot accidentally reuse rows from another test.

`CreateService` builds a `DocumentService` with real workflow dependencies where useful and fake dependencies where external systems would make the test slow or fragile:

- `DocumentDbContext` is real but stored in memory for testing purposes, so Entity Framework queries and persistence are exercised.
- `RecordingStorage` replaces Blob Storage and records save/open behavior in memory.
- `RecordingActivityMonitor` replaces Application Insights and records telemetry calls in lists.
- `MemoryCache` and `DocumentSearchCacheVersion` are real, so cache behavior is actually tested.
- `ResiliencePipelineBuilder().Build()` creates an empty pipeline for tests; retry behavior itself is covered by configuration, while these tests focus on document workflow behavior.

`RecordingStorage` and `RecordingActivityMonitor` are test doubles. They are deliberately simple: they capture observable behavior without making assertions themselves. The test methods stay responsible for deciding what should be true.

## Test Search Cache Behavior

The cache test proves that repeated searches return the same result while reporting the second call as a cache hit. It uses Entity Framework Core InMemory for metadata and the recording activity monitor to inspect the business signal.

The setup creates one document directly in the database because this test is not about upload. It is about search behavior. By seeding only the metadata needed for the query, the test stays focused on the cache path.

Open `DocumentServiceTests.cs` and implement the `SearchUsesCacheBetweenCalls` method first:

```csharp
await using var dbContext = CreateDbContext();
dbContext.Documents.Add(new Document
{
	Id = "doc-1",
	FileName = "workshop-notes.txt",
	ContentType = "text/plain",
	Size = 11,
	Title = "Workshop Notes",
	Description = "Minimal API lab",
	Source = "unit-test",
	Tags = ["lab", "notes"],
	ContentHash = Encoding.UTF8.GetBytes("hello world").Md5ToHexString(),
	CreatedUtc = DateTimeOffset.UtcNow,
});
await dbContext.SaveChangesAsync();

var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var criteria = new DocumentSearchCriteria("workshop", null, null, "text/plain");

var firstResult = await service.SearchAsync(criteria, CancellationToken.None);
var secondResult = await service.SearchAsync(criteria, CancellationToken.None);

Assert.Single(firstResult);
Assert.Single(secondResult);
Assert.Equal(2, activityMonitor.SearchEvents.Count);
Assert.False(activityMonitor.SearchEvents[0].CacheHit);
Assert.True(activityMonitor.SearchEvents[1].CacheHit);
```

The two result assertions prove that caching does not change the API result. The activity monitor assertions prove the internal behavior changed: the first call queried normally, while the second call reused the cached result.

## Test Upload Happy Path

Open `DocumentServiceTests.cs` and implement the `UploadPersistsDocumentAndTracksSuccess` test:

This test stays close to the service boundary. It uses a real Entity Framework Core context, but replaces Blob Storage and telemetry with simple in-memory doubles.

The command represents the same data the endpoint would pass after parsing multipart form data. The test intentionally calls the service directly so a failure points to the upload workflow, not to HTTP parsing, routing, or authentication.

```csharp
await using var dbContext = CreateDbContext();
var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var command = new DocumentUploadCommand(
	"notes.txt",
	"text/plain",
	new MemoryStream(Encoding.UTF8.GetBytes("hello world")),
	Encoding.UTF8.GetByteCount("hello world"),
	new DocumentMetadataDto
	{
		Title = "Workshop Notes",
		Description = "Minimal API lab",
		Source = "unit-test",
		Tags = ["lab", "notes"],
	});

var document = await service.UploadAsync(command, CancellationToken.None);

var persisted = await dbContext.Documents.AsNoTracking().SingleAsync();

Assert.Equal(document.Id, persisted.Id);
Assert.Equal("Workshop Notes", persisted.Title);
Assert.Equal(1, storage.SaveCallCount);
Assert.Single(activityMonitor.UploadSucceededDocuments);
```

These assertions cover the three important outcomes of a successful upload: metadata was persisted, the binary content was saved once, and the business telemetry hook was called. Together, they protect the full happy path without needing a real Azure Storage account.

## Test Duplicate Content

Add a document with the same hash, then upload the same bytes again:

This test protects the rule introduced in the robustness lab. If someone changes upload later, the test will catch accidental duplicate storage. Update the `UploadWithDuplicateContentThrowsAndTracksDuplicate` test with the following code at the end:

The test inserts the existing row manually with the same MD5 content hash that the upload command will produce. That lets the service exercise its duplicate check before any blob write happens.

```csharp
await using var dbContext = CreateDbContext();

var duplicateBytes = Encoding.UTF8.GetBytes("same-content");
dbContext.Documents.Add(
	new Document
	{
		Id = "existing-doc",
		FileName = "existing.txt",
		ContentType = "text/plain",
		Size = duplicateBytes.Length,
		ContentHash = duplicateBytes.Md5ToHexString(),
		CreatedUtc = DateTimeOffset.UtcNow,
	});
await dbContext.SaveChangesAsync();

var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var command = new DocumentUploadCommand(
	"incoming.txt",
	"text/plain",
	new MemoryStream(duplicateBytes),
	duplicateBytes.Length,
	new DocumentMetadataDto());

var exception = await Assert.ThrowsAsync<DuplicateDocumentException>(() => service.UploadAsync(command, CancellationToken.None));

Assert.Equal("existing-doc", exception.ExistingDocumentId);
Assert.Equal(0, storage.SaveCallCount);
Assert.Single(activityMonitor.UploadDuplicateDocumentIds);
Assert.Equal("existing-doc", activityMonitor.UploadDuplicateDocumentIds[0]);
```

The exception assertion proves callers get the domain error used by the endpoint to return `409 Conflict`. The `SaveCallCount` assertion is just as important: duplicates must be rejected before writing the same bytes to storage again.

## Test Download Behavior

For a missing document:

Download has two important branches: the document exists or it does not. Testing both keeps the public `404` behavior reliable. Update the `DownloadReturnsNullWhenDocumentIsMissing` test with the following code at the end:

For the missing case, the database starts empty. The service should return `null` and track a not-found event instead of calling storage or throwing an exception.

```csharp
await using var dbContext = CreateDbContext();
var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var result = await service.DownloadAsync("missing", CancellationToken.None);

Assert.Null(result);
Assert.Single(activityMonitor.DownloadNotFoundDocumentIds);
Assert.Equal("missing", activityMonitor.DownloadNotFoundDocumentIds[0]);
```

This is the service-level behavior that the endpoint later translates into `404 Not Found`.

For an existing document seed storage and metadata, then read the returned stream, update the `DownloadReturnsContentWhenDocumentExists` test:

For the success case, both sides of the workflow must exist: SQL metadata points to a content hash, and fake storage contains bytes under that same hash. This mirrors the real download path without calling Azure Blob Storage.

```csharp
await using var dbContext = CreateDbContext();
var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var content = Encoding.UTF8.GetBytes("stored-content");
var contentHash = content.Md5ToHexString();
storage.Seed(contentHash, content);

dbContext.Documents.Add(
	new Document
	{
		Id = "doc-42",
		FileName = "doc-42.txt",
		ContentType = "text/plain",
		Size = content.Length,
		ContentHash = contentHash,
		CreatedUtc = DateTimeOffset.UtcNow,
	});
await dbContext.SaveChangesAsync();

var result = await service.DownloadAsync("doc-42", CancellationToken.None);

Assert.NotNull(result);
using var reader = new StreamReader(result!.Content);
var body = await reader.ReadToEndAsync();

Assert.Equal("stored-content", body);
Assert.Single(activityMonitor.DownloadSucceededDocumentIds);
Assert.Equal("doc-42", activityMonitor.DownloadSucceededDocumentIds[0]);
Assert.Empty(activityMonitor.DownloadNotFoundDocumentIds);
```

Reading the stream verifies that the returned content is not just non-null; it is the exact bytes saved in storage. The activity monitor assertions prove that the success path, not the not-found path, was recorded.


<div class="tip" data-title="Use Copilot for edge cases">

> Once the first test passes, ask Copilot to suggest additional edge cases around content type, metadata parsing, duplicate upload, and missing storage content.

</div>

## Run The Tests

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet test tests/DocumentAPI.Tests/DocumentAPI.Tests.csproj
```

<div class="task" data-title="Validation">

> The test project should pass consistently.
>
> If this is the first run, SQL Server Testcontainers may take longer while the container image is prepared.

</div>

---