namespace DocumentAPI.Services.Documents.Contracts;

/// <summary>
/// Tracks a monotonic version used to invalidate cached document search results when the catalog changes.
/// </summary>
internal sealed class DocumentSearchCacheVersion
{
    private int _version;

    /// <summary>
    /// Gets the current cache version.
    /// </summary>
    public int Current => Volatile.Read(ref _version);

    /// <summary>
    /// Increments the cache version, invalidating previously cached search results.
    /// </summary>
    public void Increment() => Interlocked.Increment(ref _version);
}
