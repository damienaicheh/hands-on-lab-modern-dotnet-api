namespace DocumentAPI.Services.Validators.Documents;

/// <summary>
/// Centralizes the document MIME types supported by the API.
/// </summary>
internal static class DocumentContentTypes
{
    /// <summary>
    /// Gets the MIME types accepted for document upload and returned for document download.
    /// </summary>
    public static IReadOnlyList<string> SupportedContentTypes { get; } =
    [
        "application/pdf",
        "text/plain",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ];

    private static readonly HashSet<string> SupportedContentTypeSet = new(SupportedContentTypes, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified MIME type is supported by the API.
    /// </summary>
    /// <param name="contentType">The MIME type to validate.</param>
    /// <returns><see langword="true" /> when the MIME type is supported; otherwise <see langword="false" />.</returns>
    public static bool IsSupported(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) && SupportedContentTypeSet.Contains(contentType);
    }
}