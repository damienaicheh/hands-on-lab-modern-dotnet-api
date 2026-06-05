namespace DocumentAPI.Options;

/// <summary>
/// Represents the settings required to store and query document metadata with Entity Framework Core on SQL Server.
/// </summary>
public sealed class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets the SQL Server connection string used for metadata persistence.
    /// The value should identify the server and database only, without embedded credentials.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional client identifier of the user-assigned managed identity used for SQL Server access.
    /// </summary>
    public string ManagedIdentityClientId { get; set; } = string.Empty;
}
