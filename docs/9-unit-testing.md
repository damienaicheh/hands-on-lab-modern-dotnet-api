# Lab 9 - Unit Testing

You now have the main API behaviors in place. In this lab, you will add automated tests so the upload, search, download, duplicate detection, and edge cases can be validated repeatedly.

The test infrastructure is already prepared. Your focus is the test intent, not the boilerplate.

Each test should explain one behavior in code: what is arranged, what action happens, and what result proves the behavior is correct.

## What You Will Learn

In this lab, you will:

- Test `DocumentService` with EF Core InMemory.
- Use a fake document storage service.
- Verify upload happy path behavior.
- Verify duplicate detection.
- Verify missing and existing downloads.
- Cover endpoint behavior through the test factory.

## Files To Open

You only need to edit these files:

- `tests/DocumentAPI.Tests/DocumentServiceTests.cs`
- `tests/DocumentAPI.Tests/DocumentApiEndpointsTests.cs`

The factory, SQL Server fixture, fake storage, packages, and internal visibility setup are already provided.

## Test Upload Happy Path

Open `DocumentServiceTests.cs` and implement the upload success test:

This test stays close to the service boundary. It uses a real EF Core context, but replaces Blob Storage and telemetry with simple in-memory doubles.

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

## Test Duplicate Content

Add a document with the same hash, then upload the same bytes again:

This test protects the rule introduced in the robustness lab. If someone changes upload later, the test will catch accidental duplicate storage.

```csharp
var duplicateBytes = Encoding.UTF8.GetBytes("same-content");
dbContext.Documents.Add(new Document
{
	Id = "existing-doc",
	FileName = "existing.txt",
	ContentType = "text/plain",
	Size = duplicateBytes.Length,
	ContentHash = duplicateBytes.Md5ToHexString(),
	CreatedUtc = DateTimeOffset.UtcNow,
});
await dbContext.SaveChangesAsync();

var exception = await Assert.ThrowsAsync<DuplicateDocumentException>(
	() => service.UploadAsync(command, CancellationToken.None));

Assert.Equal("existing-doc", exception.ExistingDocumentId);
Assert.Equal(0, storage.SaveCallCount);
```

## Test Download Behavior

For a missing document:

Download has two important branches: the document exists or it does not. Testing both keeps the public `404` behavior reliable.

```csharp
var result = await service.DownloadAsync("missing", CancellationToken.None);

Assert.Null(result);
Assert.Single(activityMonitor.DownloadNotFoundDocumentIds);
```

For an existing document, seed storage and metadata, then read the returned stream:

```csharp
var content = Encoding.UTF8.GetBytes("stored-content");
var contentHash = content.Md5ToHexString();
storage.Seed(contentHash, content);

dbContext.Documents.Add(new Document
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
```

## Test HTTP Endpoints

Open `DocumentApiEndpointsTests.cs` and add an integration test for the round trip:

Endpoint tests validate the wiring that unit tests cannot see: routing, multipart parsing, authentication headers, model serialization, and status codes.

```csharp
using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
using var client = factory.CreateClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

using var uploadContent = CreateMultipartForm(
	fileName: "notes.txt",
	contentType: "text/plain",
	body: "hello world",
	metadata: new DocumentMetadataDto { Title = "Workshop Notes" });

var uploadResponse = await client.PostAsync("/documents?api-version=1.0", uploadContent);

Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
```

Use the same pattern for missing metadata, duplicate upload, and unknown download id.

<div class="tip" data-title="Use Copilot for edge cases">

> Once the first test passes, ask Copilot to suggest additional edge cases around content type, metadata parsing, duplicate upload, and missing storage content.

</div>

## Run The Tests

```bash
dotnet test tests/DocumentAPI.Tests/DocumentAPI.Tests.csproj
```

<div class="task" data-title="Validation">

> The test project should pass consistently.
>
> If this is the first run, SQL Server Testcontainers may take longer while the container image is prepared.

</div>

---