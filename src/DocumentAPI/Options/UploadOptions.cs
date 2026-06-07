namespace DocumentAPI.Options;

/// <summary>
/// Represents upload-specific validation settings.
/// </summary>
public sealed class UploadOptions
{
    /// <summary>
    /// Gets or sets the maximum accepted file size in bytes.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}