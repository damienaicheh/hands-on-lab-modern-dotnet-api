namespace DocumentAPI.Services.Monitoring;

using DocumentAPI.DTOs;
using DocumentAPI.Services.Documents.Contracts;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

/// <summary>
/// Emits business-level document activity to Application Insights and structured logs.
/// </summary>
/// <param name="telemetryClient">The telemetry client used to push custom events and metrics.</param>
/// <param name="logger">The logger used to emit structured diagnostic entries.</param>
public sealed class ApplicationInsightsDocumentActivityMonitor(
    TelemetryClient telemetryClient,
    ILogger<ApplicationInsightsDocumentActivityMonitor> logger) : IDocumentActivityMonitor
{
    private readonly TelemetryClient _telemetryClient = telemetryClient;
    private readonly ILogger<ApplicationInsightsDocumentActivityMonitor> _logger = logger;

    /// <inheritdoc />
    public void TrackSearch(DocumentSearchCriteria criteria, int resultCount, bool cacheHit)
    {
        // <lab id="12">
        //|        // TODO Lab 12: Emit a structured log and Application Insights event for document searches.
        //|        return;
        _logger.LogInformation(
            "Document search completed. CacheHit={CacheHit} ResultCount={ResultCount} HasQuery={HasQuery} HasTitleFilter={HasTitleFilter} HasTagFilter={HasTagFilter} HasContentTypeFilter={HasContentTypeFilter}",
            cacheHit,
            resultCount,
            !string.IsNullOrWhiteSpace(criteria.Query),
            !string.IsNullOrWhiteSpace(criteria.Title),
            !string.IsNullOrWhiteSpace(criteria.Tag),
            !string.IsNullOrWhiteSpace(criteria.ContentType));

        _telemetryClient.TrackEvent(
            "Documents.Search.Completed",
            new Dictionary<string, string>
            {
                ["CacheHit"] = cacheHit.ToString(),
                ["HasQuery"] = (!string.IsNullOrWhiteSpace(criteria.Query)).ToString(),
                ["HasTitleFilter"] = (!string.IsNullOrWhiteSpace(criteria.Title)).ToString(),
                ["HasTagFilter"] = (!string.IsNullOrWhiteSpace(criteria.Tag)).ToString(),
                ["HasContentTypeFilter"] = (!string.IsNullOrWhiteSpace(criteria.ContentType)).ToString(),
            },
            new Dictionary<string, double>
            {
                ["ResultCount"] = resultCount,
            });
        // </lab>
    }

    /// <inheritdoc />
    public void TrackUploadSucceeded(DocumentDto document, double durationMs)
    {
        // <lab id="12">
        //|        // TODO Lab 12: Emit upload success telemetry.
        //|        return;
        _logger.LogInformation(
            "Document upload completed. DocumentId={DocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
            document.Id,
            document.ContentType,
            document.Size,
            durationMs);

        _telemetryClient.TrackEvent(
            "Documents.Upload.Completed",
            new Dictionary<string, string>
            {
                ["DocumentId"] = document.Id,
                ["ContentType"] = document.ContentType ?? string.Empty,
            },
            new Dictionary<string, double>
            {
                ["SizeBytes"] = document.Size ?? 0,
                ["DurationMs"] = durationMs,
            });

        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.SizeBytes", document.Size ?? 0));
        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.DurationMs", durationMs));
        // </lab>
    }

    /// <inheritdoc />
    public void TrackUploadDuplicate(string existingDocumentId, string contentType, long sizeBytes, double durationMs)
    {
        // <lab id="12">
        //|        // TODO Lab 12: Emit duplicate upload telemetry.
        //|        return;
        _logger.LogWarning(
            "Duplicate document upload rejected. ExistingDocumentId={ExistingDocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
            existingDocumentId,
            contentType,
            sizeBytes,
            durationMs);

        _telemetryClient.TrackEvent(
            "Documents.Upload.Duplicate",
            new Dictionary<string, string>
            {
                ["ExistingDocumentId"] = existingDocumentId,
                ["ContentType"] = contentType,
            },
            new Dictionary<string, double>
            {
                ["SizeBytes"] = sizeBytes,
                ["DurationMs"] = durationMs,
            });

        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.DuplicateCount", 1));
        // </lab>
    }

    /// <inheritdoc />
    public void TrackDownloadSucceeded(string documentId, string contentType, long sizeBytes, double durationMs)
    {
        // <lab id="12">
        //|        // TODO Lab 12: Emit download success telemetry.
        //|        return;
        _logger.LogInformation(
            "Document download completed. DocumentId={DocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
            documentId,
            contentType,
            sizeBytes,
            durationMs);

        _telemetryClient.TrackEvent(
            "Documents.Download.Completed",
            new Dictionary<string, string>
            {
                ["DocumentId"] = documentId,
                ["ContentType"] = contentType,
            },
            new Dictionary<string, double>
            {
                ["SizeBytes"] = sizeBytes,
                ["DurationMs"] = durationMs,
            });

        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.SizeBytes", sizeBytes));
        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.DurationMs", durationMs));
        // </lab>
    }

    /// <inheritdoc />
    public void TrackDownloadNotFound(string documentId, double durationMs)
    {
        // <lab id="12">
        //|        // TODO Lab 12: Emit download miss telemetry.
        //|        return;
        _logger.LogWarning(
            "Document download returned no content. DocumentId={DocumentId} DurationMs={DurationMs}",
            documentId,
            durationMs);

        _telemetryClient.TrackEvent(
            "Documents.Download.NotFound",
            new Dictionary<string, string>
            {
                ["DocumentId"] = documentId,
            },
            new Dictionary<string, double>
            {
                ["DurationMs"] = durationMs,
            });

        _telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.NotFoundCount", 1));
        // </lab>
    }
}