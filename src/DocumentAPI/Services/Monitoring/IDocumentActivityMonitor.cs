namespace DocumentAPI.Services.Monitoring;

using DocumentAPI.DTOs;
using DocumentAPI.Services.Documents;

/// <summary>
/// Defines the business-level telemetry emitted for document operations.
/// </summary>
public interface IDocumentActivityMonitor
{
    /// <summary>
    /// Tracks the completion of a document search request.
    /// </summary>
    /// <param name="criteria">The search criteria used for the request.</param>
    /// <param name="resultCount">The number of matching documents returned.</param>
    /// <param name="cacheHit">A value indicating whether the result came from cache.</param>
    void TrackSearch(DocumentSearchCriteria criteria, int resultCount, bool cacheHit);

    /// <summary>
    /// Tracks the successful completion of a document upload.
    /// </summary>
    /// <param name="document">The created document representation.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    void TrackUploadSucceeded(DocumentDto document, double durationMs);

    /// <summary>
    /// Tracks a rejected duplicate document upload.
    /// </summary>
    /// <param name="existingDocumentId">The identifier of the existing matching document.</param>
    /// <param name="contentType">The uploaded document content type.</param>
    /// <param name="sizeBytes">The uploaded document size in bytes.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    void TrackUploadDuplicate(string existingDocumentId, string contentType, long sizeBytes, double durationMs);

    /// <summary>
    /// Tracks the successful completion of a document download.
    /// </summary>
    /// <param name="documentId">The downloaded document identifier.</param>
    /// <param name="contentType">The document content type.</param>
    /// <param name="sizeBytes">The document size in bytes.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    void TrackDownloadSucceeded(string documentId, string contentType, long sizeBytes, double durationMs);

    /// <summary>
    /// Tracks a document download request that returned no content.
    /// </summary>
    /// <param name="documentId">The requested document identifier.</param>
    /// <param name="durationMs">The elapsed duration in milliseconds.</param>
    void TrackDownloadNotFound(string documentId, double durationMs);
}
