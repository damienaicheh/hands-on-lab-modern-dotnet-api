namespace DocumentAPI.Services.Documents;

using DocumentAPI.DTOs;
using DocumentAPI.Services.Documents.Contracts;

/// <summary>
/// Defines the core document operations exposed to the API layer.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Searches for documents that match the provided criteria.
    /// </summary>
    /// <param name="criteria">The search criteria.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The matching documents.</returns>
    Task<IReadOnlyList<DocumentDto>> SearchAsync(DocumentSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a document and persists its content and metadata.
    /// </summary>
    /// <param name="command">The upload command payload.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The created document.</returns>
    Task<DocumentDto> UploadAsync(DocumentUploadCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the binary content of a document by identifier.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The downloaded document content when found; otherwise <see langword="null" />.</returns>
    Task<DocumentContentResult?> DownloadAsync(string id, CancellationToken cancellationToken);
}
