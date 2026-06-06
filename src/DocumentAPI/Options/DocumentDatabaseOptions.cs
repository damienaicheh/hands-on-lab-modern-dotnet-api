namespace DocumentAPI.Options;

/// <summary>
/// Represents the settings required to store and query document metadata with Entity Framework Core on SQL Server.
/// </summary>
public sealed class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets the SQL Database server URI used for metadata persistence.
    /// Use the format <c>https://&lt;sql-server-name&gt;.database.windows.net</c>.
    /// </summary>
    public string ServiceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SQL Database name used for metadata persistence.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
