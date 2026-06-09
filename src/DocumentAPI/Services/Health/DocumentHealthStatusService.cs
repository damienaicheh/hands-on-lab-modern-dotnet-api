namespace DocumentAPI.Services.Health;

using DocumentAPI.Persistence;
using DocumentAPI.Services.Health.Contracts;
using DocumentAPI.Services.Storage;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Computes the service health state from the configured storage and database providers.
/// </summary>
internal sealed class DocumentHealthStatusService(IDocumentStorageService storage, DocumentDbContext dbContext) : IHealthStatusService
{
    private readonly IDocumentStorageService _storage = storage;
    private readonly DocumentDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<HealthStateResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        // <lab id="8">
        //|        // TODO Lab 8: Check SQL and Blob Storage connectivity and return the evaluated state.
        //|        throw new NotImplementedException("TODO Lab 8: Check SQL and Blob Storage connectivity and return the evaluated state.");
        var storageHealthy = await _storage.CanConnectAsync(cancellationToken);
        var databaseHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
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
}