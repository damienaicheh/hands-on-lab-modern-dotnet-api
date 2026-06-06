namespace DocumentAPI.Services.Documents;

using System.Diagnostics;
using System.Security.Cryptography;
using DocumentAPI.DTOs;
using DocumentAPI.Entities;
using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Documents.Exceptions;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly;

/// <summary>
/// Implements the document workflow used by the API.
/// </summary>
internal sealed class DocumentService(
    DocumentDbContext dbContext,
    IDocumentStorage storage,
    IDocumentActivityMonitor activityMonitor,
    IMemoryCache cache,
    DocumentSearchCacheVersion cacheVersion,
    ResiliencePipeline resiliencePipeline,
    IOptions<DocumentApiOptions> options) : IDocumentService
{
    private readonly DocumentDbContext _dbContext = dbContext;
    private readonly IDocumentStorage _storage = storage;
    private readonly IDocumentActivityMonitor _activityMonitor = activityMonitor;
    private readonly IMemoryCache _cache = cache;
    private readonly DocumentSearchCacheVersion _cacheVersion = cacheVersion;
    private readonly ResiliencePipeline _resiliencePipeline = resiliencePipeline;
    private readonly DocumentApiOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentDto>> SearchAsync(DocumentSearchCriteria criteria, CancellationToken cancellationToken)
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
    }

    /// <inheritdoc />
    public async Task<DocumentDto> UploadAsync(DocumentUploadCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var hash = ComputeContentHash(command.Content);
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
                command.Content.LongLength,
                stopwatch.Elapsed.TotalMilliseconds);
            throw new DuplicateDocumentException(existingDocument.Id);
        }

        var documentId = Guid.NewGuid().ToString("N");
        var blobUploaded = false;

        try
        {
            await _storage.SaveAsync(hash, command.Content, cancellationToken);
            blobUploaded = true;

            var (title, description, source, tags) = NormalizeMetadata(command.Metadata);
            var document = new Document
            {
                Id = documentId,
                FileName = command.FileName,
                ContentType = command.ContentType,
                Size = command.Content.LongLength,
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
            _cacheVersion.Increment();

            var documentDto = ToDocumentDto(document);
            stopwatch.Stop();
            _activityMonitor.TrackUploadSucceeded(documentDto, stopwatch.Elapsed.TotalMilliseconds);
            return documentDto;
        }
        catch (DbUpdateException)
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
                    await _storage.DeleteAsync(hash, cancellationToken);
                }

                throw;
            }

            stopwatch.Stop();
            _activityMonitor.TrackUploadDuplicate(
                conflictingDocument.Id,
                command.ContentType,
                command.Content.LongLength,
                stopwatch.Elapsed.TotalMilliseconds);
            throw new DuplicateDocumentException(conflictingDocument.Id);
        }
    }

    /// <inheritdoc />
    public async Task<DocumentContentResult?> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
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

    /// <summary>
    /// Computes the MD5 content hash used to detect duplicate documents.
    /// </summary>
    private static string ComputeContentHash(byte[] content)
    {
        // MD5 is used to align the duplicate-detection hash with the Content-MD5 integrity contract, not for security.
#pragma warning disable CA5351
        return Convert.ToHexString(MD5.HashData(content));
#pragma warning restore CA5351
    }

    /// <summary>
    /// Queries documents that match the provided criteria.
    /// </summary>
    private async Task<IReadOnlyList<DocumentDto>> QueryDocumentsAsync(DocumentSearchCriteria criteria, CancellationToken cancellationToken)
    {
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
    }

    /// <summary>
    /// Converts a document entity into the public API contract.
    /// </summary>
    private static DocumentDto ToDocumentDto(Document document)
    {
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
