namespace DocumentAPI.Tests;

using System.Security.Cryptography;
using System.Text;
using DocumentAPI.DTOs;
using DocumentAPI.Entities;
using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Helpers;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Documents.Exceptions;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly;

/// <summary>
/// Verifies the document domain behavior implemented by <see cref="DocumentService" />.
/// </summary>
public sealed class DocumentServiceTests
{
    /// <summary>
    /// Verifies that repeated searches with the same criteria use the in-memory cache.
    /// </summary>
    [Fact]
    public async Task SearchUsesCacheBetweenCalls()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Documents.Add(
            new Document
            {
                Id = "doc-1",
                FileName = "workshop-notes.txt",
                ContentType = "text/plain",
                Size = 11,
                Title = "Workshop Notes",
                Description = "Minimal API lab",
                Source = "unit-test",
                Tags = ["lab", "notes"],
                ContentHash = FileHelper.ComputeContentHash(Encoding.UTF8.GetBytes("hello world")),
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
    }

    /// <summary>
    /// Verifies that upload persists document metadata and emits success telemetry.
    /// </summary>
    [Fact]
    public async Task UploadPersistsDocumentAndTracksSuccess()
    {
        await using var dbContext = CreateDbContext();
        var storage = new RecordingStorage();
        var activityMonitor = new RecordingActivityMonitor();
        var service = CreateService(dbContext, storage, activityMonitor);

        var command = new DocumentUploadCommand(
            "notes.txt",
            "text/plain",
            Encoding.UTF8.GetBytes("hello world"),
            new DocumentMetadataDto
            {
                Title = "  Workshop Notes  ",
                Description = "  Minimal API lab  ",
                Source = "  unit-test  ",
                Tags = [" lab ", "LAB", "notes", ""],
            });

        var document = await service.UploadAsync(command, CancellationToken.None);

        var persisted = await dbContext.Documents.AsNoTracking().SingleAsync();

        Assert.Equal(document.Id, persisted.Id);
        Assert.Equal("Workshop Notes", persisted.Title);
        Assert.Equal("Minimal API lab", persisted.Description);
        Assert.Equal("unit-test", persisted.Source);
        Assert.Equal(["lab", "notes"], persisted.Tags);
        Assert.Equal(1, storage.SaveCallCount);
        Assert.Single(activityMonitor.UploadSucceededDocuments);
        Assert.Equal(document.Id, activityMonitor.UploadSucceededDocuments[0].Id);
    }

    /// <summary>
    /// Verifies that duplicate content is rejected before storage write and tracked as duplicate telemetry.
    /// </summary>
    [Fact]
    public async Task UploadWithDuplicateContentThrowsAndTracksDuplicate()
    {
        await using var dbContext = CreateDbContext();

        var duplicateBytes = Encoding.UTF8.GetBytes("same-content");
        dbContext.Documents.Add(
            new Document
            {
                Id = "existing-doc",
                FileName = "existing.txt",
                ContentType = "text/plain",
                Size = duplicateBytes.Length,
                ContentHash = FileHelper.ComputeContentHash(duplicateBytes),
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        await dbContext.SaveChangesAsync();

        var storage = new RecordingStorage();
        var activityMonitor = new RecordingActivityMonitor();
        var service = CreateService(dbContext, storage, activityMonitor);

        var command = new DocumentUploadCommand(
            "incoming.txt",
            "text/plain",
            duplicateBytes,
            new DocumentMetadataDto());

        var exception = await Assert.ThrowsAsync<DuplicateDocumentException>(() => service.UploadAsync(command, CancellationToken.None));

        Assert.Equal("existing-doc", exception.ExistingDocumentId);
        Assert.Equal(0, storage.SaveCallCount);
        Assert.Single(activityMonitor.UploadDuplicateDocumentIds);
        Assert.Equal("existing-doc", activityMonitor.UploadDuplicateDocumentIds[0]);
    }

    /// <summary>
    /// Verifies that downloading an unknown identifier returns null and emits not-found telemetry.
    /// </summary>
    [Fact]
    public async Task DownloadReturnsNullWhenDocumentIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var storage = new RecordingStorage();
        var activityMonitor = new RecordingActivityMonitor();
        var service = CreateService(dbContext, storage, activityMonitor);

        var result = await service.DownloadAsync("missing", CancellationToken.None);

        Assert.Null(result);
        Assert.Single(activityMonitor.DownloadNotFoundDocumentIds);
        Assert.Equal("missing", activityMonitor.DownloadNotFoundDocumentIds[0]);
    }

    /// <summary>
    /// Verifies that downloading an existing identifier returns content and emits success telemetry.
    /// </summary>
    [Fact]
    public async Task DownloadReturnsContentWhenDocumentExists()
    {
        await using var dbContext = CreateDbContext();
        var storage = new RecordingStorage();
        var activityMonitor = new RecordingActivityMonitor();
        var service = CreateService(dbContext, storage, activityMonitor);

        var content = Encoding.UTF8.GetBytes("stored-content");
        var contentHash = FileHelper.ComputeContentHash(content);
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
    }

    private static IDocumentService CreateService(
        DocumentDbContext dbContext,
        RecordingStorage storage,
        RecordingActivityMonitor activityMonitor)
    {
        var options = Options.Create(
            new DocumentApiOptions
            {
                Search = new SearchOptions
                {
                    CacheTtlSeconds = 60,
                },
            });

        return new DocumentService(
            dbContext,
            storage,
            activityMonitor,
            new MemoryCache(new MemoryCacheOptions()),
            new DocumentSearchCacheVersion(),
            new ResiliencePipelineBuilder().Build(),
            options);
    }

    private static DocumentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase($"DocumentServiceTests-{Guid.NewGuid():N}")
            .Options;

        return new DocumentDbContext(options);
    }

    private sealed class RecordingActivityMonitor : IDocumentActivityMonitor
    {
        public List<(DocumentSearchCriteria Criteria, int ResultCount, bool CacheHit)> SearchEvents { get; } = [];

        public List<DocumentDto> UploadSucceededDocuments { get; } = [];

        public List<string> UploadDuplicateDocumentIds { get; } = [];

        public List<string> DownloadSucceededDocumentIds { get; } = [];

        public List<string> DownloadNotFoundDocumentIds { get; } = [];

        public void TrackSearch(DocumentSearchCriteria criteria, int resultCount, bool cacheHit)
        {
            SearchEvents.Add((criteria, resultCount, cacheHit));
        }

        public void TrackUploadSucceeded(DocumentDto document, double durationMs)
        {
            UploadSucceededDocuments.Add(document);
        }

        public void TrackUploadDuplicate(string existingDocumentId, string contentType, long sizeBytes, double durationMs)
        {
            UploadDuplicateDocumentIds.Add(existingDocumentId);
        }

        public void TrackDownloadSucceeded(string documentId, string contentType, long sizeBytes, double durationMs)
        {
            DownloadSucceededDocumentIds.Add(documentId);
        }

        public void TrackDownloadNotFound(string documentId, double durationMs)
        {
            DownloadNotFoundDocumentIds.Add(documentId);
        }
    }

    private sealed class RecordingStorage : IDocumentStorage
    {
        private readonly Dictionary<string, byte[]> _contentByKey = new(StringComparer.Ordinal);

        public int SaveCallCount { get; private set; }

        public Task SaveAsync(string contentHash, byte[] content, CancellationToken cancellationToken)
        {
            SaveCallCount++;
            _contentByKey[contentHash] = [.. content];
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string contentHash, CancellationToken cancellationToken)
        {
            _contentByKey.Remove(contentHash);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken)
        {
            if (!_contentByKey.TryGetValue(contentHash, out var content))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new MemoryStream([.. content], writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public void Seed(string contentHash, byte[] content)
        {
            _contentByKey[contentHash] = [.. content];
        }
    }
}
