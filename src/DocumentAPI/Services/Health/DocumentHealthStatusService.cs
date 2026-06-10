namespace DocumentAPI.Services.Health;

using DocumentAPI.Persistence;
using DocumentAPI.Services.Health.Contracts;
using DocumentAPI.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Computes the service health state from the configured storage and database providers.
/// </summary>
internal sealed class DocumentHealthStatusService(
    IDocumentStorageService storage,
    DocumentDbContext dbContext,
    IMemoryCache cache) : IHealthStatusService
{
    private static readonly TimeSpan ConnectivityCheckInterval = TimeSpan.FromMinutes(1);
    private const string StorageConnectivityCacheKey = "health:connectivity:storage";
    private const string DatabaseConnectivityCacheKey = "health:connectivity:database";

    private readonly IDocumentStorageService _storage = storage;
    private readonly DocumentDbContext _dbContext = dbContext;
    private readonly IMemoryCache _cache = cache;

    /// <inheritdoc />
    public async Task<HealthStateResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        // <lab id="8">
        //|        // TODO Lab 8: Check SQL and Blob Storage connectivity and return the evaluated state.
        //|        throw new NotImplementedException("TODO Lab 8: Check SQL and Blob Storage connectivity and return the evaluated state.");
        var storageHealthy = await GetCachedConnectivityAsync(
            StorageConnectivityCacheKey,
            token => _storage.CanConnectAsync(token),
            cancellationToken);
        var databaseHealthy = await GetCachedConnectivityAsync(
            DatabaseConnectivityCacheKey,
            token => _dbContext.Database.CanConnectAsync(token),
            cancellationToken);
        var checks = new Dictionary<string, HealthDependencyState>(StringComparer.Ordinal)
        {
            ["database"] = databaseHealthy
                ? new HealthDependencyState(HealthStatus.Healthy)
                : new HealthDependencyState(HealthStatus.Unhealthy, "Database is unreachable."),
            ["storage"] = storageHealthy
                ? new HealthDependencyState(HealthStatus.Healthy)
                : new HealthDependencyState(HealthStatus.Unhealthy, "Storage is unreachable."),
        };

        if (storageHealthy && databaseHealthy)
        {
            return new HealthStateResult(HealthStatus.Healthy, true, checks);
        }

        if (storageHealthy || databaseHealthy)
        {
            return new HealthStateResult(HealthStatus.Degraded, true, checks);
        }

        return new HealthStateResult(HealthStatus.Unhealthy, false, checks);
        // </lab>
    }

    private async Task<bool> GetCachedConnectivityAsync(
        string cacheKey,
        Func<CancellationToken, Task<bool>> connectivityCheck,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out bool isHealthy))
        {
            return isHealthy;
        }

        try
        {
            isHealthy = await connectivityCheck(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            isHealthy = false;
        }

        _cache.Set(
            cacheKey,
            isHealthy,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ConnectivityCheckInterval,
            });

        return isHealthy;
    }
}