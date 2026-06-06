namespace DocumentAPI.Persistence;

/// <summary>
/// Centralizes schema and table names used by persistence mappings and migrations.
/// </summary>
internal static class PersistenceModelConstants
{
    /// <summary>
    /// Gets the default schema used for relational objects.
    /// </summary>
    public const string DefaultSchema = "dbo";

    /// <summary>
    /// Gets the table name used to persist document metadata.
    /// </summary>
    public const string DocumentsTable = "Documents";
}