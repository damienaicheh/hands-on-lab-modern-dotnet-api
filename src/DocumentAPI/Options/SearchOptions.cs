namespace DocumentAPI.Options;

/// <summary>
/// Represents search-specific runtime settings.
/// </summary>
public sealed class SearchOptions
{
    /// <summary>
    /// Gets or sets the in-memory cache time-to-live in seconds for search results.
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 60;
}