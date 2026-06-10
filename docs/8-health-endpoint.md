# Lab 8 - Health Endpoint

The API now depends on SQL Server and Blob Storage. In this lab, you will expose a health endpoint that reports whether those dependencies are reachable.

Health endpoints are used by humans, deployment systems, and monitoring tools. They should be simple, stable, and safe to call without authentication.

The endpoint is not meant to expose private diagnostics. It gives just enough information to know whether the API should receive traffic.

## What You Will Learn

In this lab, you will:

- Check database connectivity.
- Check Blob Storage connectivity.
- Re-evaluate dependency connectivity on a short cache interval instead of relying on a startup-only result.
- Return `Healthy`, `Degraded`, or `Unhealthy`.
- Include per-dependency details when the service is degraded.
- Keep `/health` anonymous.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Health/DocumentHealthStatusService.cs`
- `src/DocumentAPI/Endpoints/HealthEndpoints.cs`

The health contracts, response models, and DI registration are already provided.

## Evaluate Dependency Health

Open `DocumentHealthStatusService.cs` and implement `GetStatusAsync`:

A health endpoint should check the dependencies that make the API useful. Here, the service is healthy only when both SQL metadata and Blob content access are available.

The service includes a small in-memory cache around connectivity probes. This keeps `/health` inexpensive while still refreshing the dependency state periodically when the endpoint is called.

```csharp
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
```

Then return the overall state:

```csharp
if (storageHealthy && databaseHealthy)
{
	return new HealthStateResult(HealthStatus.Healthy, true, checks);
}

if (storageHealthy || databaseHealthy)
{
	return new HealthStateResult(HealthStatus.Degraded, true, checks);
}

return new HealthStateResult(HealthStatus.Unhealthy, false, checks);
```

## Map Health To HTTP

Open `HealthEndpoints.cs` and implement the response mapping:

The response has two layers: an HTTP status for infrastructure tools and a body that gives humans or dashboards more detail.

```csharp
var status = await healthStatusService.GetStatusAsync(cancellationToken);

if (!status.IsAvailable)
{
	return Results.Json(
		new UnhealthyStatus { Status = status.Status.ToString() },
		statusCode: StatusCodes.Status503ServiceUnavailable);
}

if (status.Status != HealthStatus.Degraded)
{
	return Results.Ok(new HealthyOrDegradedStatus { Status = status.Status.ToString() });
}
```

For degraded mode, include dependency details:

`Degraded` is useful when the service is still reachable but not fully healthy. It gives operators a clear signal without pretending everything is fine.

```csharp
return Results.Ok(new HealthyOrDegradedStatus
{
	Status = status.Status.ToString(),
	Checks = status.Checks.ToDictionary(
		pair => pair.Key,
		pair => new HealthCheckStatus
		{
			Status = pair.Value.Status.ToString(),
			Description = pair.Value.Description,
		},
		StringComparer.Ordinal),
});
```

<div class="tip" data-title="Why health is anonymous">

> Monitoring systems often call health endpoints without user credentials. Later, when JWT authentication is added, `/health` will remain public.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Call `/health` and confirm that it returns a status value.
>
> If one dependency is unavailable, the response should be `Degraded` and include dependency details.

</div>

---