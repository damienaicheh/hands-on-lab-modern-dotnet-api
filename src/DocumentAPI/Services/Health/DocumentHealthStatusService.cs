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
        var storageHealthy = await _storage.CanConnectAsync(cancellationToken);
        var databaseHealthy = await _dbContext.Database.CanConnectAsync(cancellationToken);
        var checks = new Dictionary<string, HealthDependencyState>(StringComparer.Ordinal)
        {
            ["database"] = databaseHealthy
                ? new HealthDependencyState("Healthy")
                : new HealthDependencyState("Unhealthy", "Database is unreachable."),
            ["storage"] = storageHealthy
                ? new HealthDependencyState("Healthy")
                : new HealthDependencyState("Unhealthy", "Storage is unreachable."),
        };

        if (storageHealthy && databaseHealthy)
        {
            return new HealthStateResult("Healthy", true, checks);
        }

        if (storageHealthy || databaseHealthy)
        {
            return new HealthStateResult("Degraded", true, checks);
        }

        return new HealthStateResult("Unhealthy", false, checks);
    }
}