namespace DocumentAPI.Options;

/// <summary>
/// Represents the root configuration section for the Document API.
/// </summary>
public sealed class DocumentApiOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind the document API options.
    /// </summary>
    public const string SectionName = "DocumentApi";

    /// <summary>
    /// Gets or sets the authentication settings for the API.
    /// </summary>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the monitoring settings for the API.
    /// </summary>
    public MonitoringOptions Monitoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the upload validation settings.
    /// </summary>
    public UploadOptions Upload { get; set; } = new();

    /// <summary>
    /// Gets or sets the binary document storage settings.
    /// </summary>
    public DocumentStorageOptions Storage { get; set; } = new();

    /// <summary>
    /// Gets or sets the document metadata database settings.
    /// </summary>
    public DocumentDatabaseOptions Database { get; set; } = new();

    /// <summary>
    /// Gets or sets the search behavior settings.
    /// </summary>
    public SearchOptions Search { get; set; } = new();
}