namespace DocumentAPI.Services.Documents;

using Azure;
using System.Diagnostics;
using DocumentAPI.DTOs;
using DocumentAPI.Entities;
using DocumentAPI.Extensions;
using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Documents.Exceptions;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using DocumentAPI.Services.Documents.Contracts;

/// <summary>
/// Implements the document workflow used by the API.
/// </summary>
internal sealed class DocumentService(
    DocumentDbContext dbContext,
    IDocumentStorageService storage,
    IDocumentActivityMonitor activityMonitor,
    IMemoryCache cache,
    DocumentSearchCacheVersion cacheVersion,
    ResiliencePipeline resiliencePipeline,
    IOptions<DocumentApiOptions> options,
    ILogger<DocumentService> logger) : IDocumentService
{
    private readonly DocumentDbContext _dbContext = dbContext;
    private readonly IDocumentStorageService _storage = storage;
    private readonly IDocumentActivityMonitor _activityMonitor = activityMonitor;
    private readonly ILogger<DocumentService> _logger = logger;
    private readonly IMemoryCache _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
    private readonly DocumentSearchCacheVersion _cacheVersion = cacheVersion ?? new DocumentSearchCacheVersion();
    private readonly ResiliencePipeline _resiliencePipeline = resiliencePipeline ?? DocumentResiliencePipeline.Create();
    private readonly DocumentApiOptions _options = options?.Value ?? new DocumentApiOptions();

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentDto>> SearchAsync(DocumentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // <lab id="6">
        //|        throw new NotImplementedException("TODO Lab 6: Implement document search.");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // <lab id="7">
            //|            throw new NotImplementedException("TODO Lab 7: Add caching around document search.");
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
        // </lab>
    }

    /// <inheritdoc />
    public async Task<DocumentDto> UploadAsync(DocumentUploadCommand command, CancellationToken cancellationToken)
    {
        // <lab id="4">
        //|        throw new NotImplementedException("TODO Lab 4: Implement the document upload happy path.");
        if (!command.Content.CanSeek)
        {
            throw new ArgumentException("The upload content stream must support seeking.", nameof(command));
        }

        var stopwatch = Stopwatch.StartNew();
        var md5 = command.Content.ComputeMd5();
        var hash = Convert.ToHexString(md5);
        command.Content.Position = 0;

        // <lab id="5">
        //|        throw new NotImplementedException("TODO Lab 5: Add duplicate detection, retries, rollback, and clean error handling.");
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
            await _resiliencePipeline.ExecuteAsync(
                async token => await _dbContext.SaveChangesAsync(token),
                cancellationToken);
            // <lab id="7">
            //|            // TODO Lab 7: Invalidate cached search results after a successful upload.
            _cacheVersion.Increment();
            // </lab>

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
        // </lab>
        // </lab>
    }

    /// <inheritdoc />
    public async Task<DocumentContentResult?> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        // <lab id="6">
        //|        throw new NotImplementedException("TODO Lab 6: Implement document download.");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var document = await _resiliencePipeline.ExecuteAsync(
                async token => await _dbContext.Documents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Id == id, token),
                cancellationToken);

            if (document is null)
            {
                stopwatch.Stop();
                _activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
                return null;
            }

            var stream = await _storage.OpenReadAsync(document.ContentHash, cancellationToken);

            if (stream is null)
            {
                stopwatch.Stop();
                _activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
                return null;
            }

            stopwatch.Stop();
            _activityMonitor.TrackDownloadSucceeded(document.Id, document.ContentType, document.Size, stopwatch.Elapsed.TotalMilliseconds);
            return new DocumentContentResult(document.FileName, document.ContentType, stream);
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
        // </lab>
    }

    /// <summary>
    /// Queries documents that match the provided criteria.
    /// </summary>
    private async Task<IReadOnlyList<DocumentDto>> QueryDocumentsAsync(DocumentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // <lab id="6">
        //|    throw new NotImplementedException("TODO Lab 6: Query documents by optional search criteria.");
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
        // </lab>
    }

    /// <summary>
    /// Converts a document entity into the public API contract.
    /// </summary>
    private static DocumentDto ToDocumentDto(Document document)
    {
        // <lab id="4">
        //|    throw new NotImplementedException("TODO Lab 4: Map the entity to the public DTO.");
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
        // </lab>
    }

    /// <summary>
    /// Normalizes incoming document metadata before it is persisted.
    /// </summary>
    private static (string? Title, string? Description, string? Source, List<string> Tags) NormalizeMetadata(DocumentMetadataDto metadata)
    {
        var normalizedTags = metadata.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return (
            metadata.Title?.Trim(),
            metadata.Description?.Trim(),
            metadata.Source?.Trim(),
            normalizedTags);
    }

    /// <summary>
    /// Determines whether a document matches a free-text query across its searchable fields.
    /// </summary>
    private static bool ContainsFreeText(Document document, string query)
    {
        return Contains(document.Title, query)
            || Contains(document.Description, query)
            || Contains(document.Source, query)
            || document.Tags.Any(tag => Contains(tag, query));
    }

    /// <summary>
    /// Determines whether a candidate value contains the query using a case-insensitive comparison.
    /// </summary>
    private static bool Contains(string? candidate, string query)
    {
        return !string.IsNullOrWhiteSpace(candidate)
            && candidate.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a deterministic cache key for the current search criteria.
    /// </summary>
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

    /// <summary>
    /// Normalizes a cache key segment for consistent cache lookups.
    /// </summary>
    private static string NormalizeCacheSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
