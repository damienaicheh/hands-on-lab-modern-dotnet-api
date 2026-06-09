# Lab 12 - Observability: Correlation ID and Application Insights

In the final lab, you will make the API easier to troubleshoot. You will add request correlation, HTTP logging, Application Insights telemetry, and business-level document activity monitoring.

The goal is to understand what happened, where it happened, and which document operation was involved.

## What You Will Learn

In this lab, you will:

- Read or generate an `X-Correlation-Id` header.
- Echo the correlation id in the response.
- Add the correlation id to logging scope and telemetry.
- Register Application Insights.
- Emit custom events and metrics for upload, search, and download.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Observability/CorrelationIdMiddleware.cs`
- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Services/Monitoring/ApplicationInsightsDocumentActivityMonitor.cs`

The telemetry initializer, monitoring options, and monitor interface are already provided.

## Add Correlation ID Middleware

Open `CorrelationIdMiddleware.cs` and implement `InvokeAsync`:

A correlation id is the thread you can follow through logs, HTTP responses, and telemetry. If the caller already sends one, the API keeps it; otherwise it creates one.

```csharp
var correlationId = ResolveCorrelationId(context.Request.Headers);
context.TraceIdentifier = correlationId;
context.Response.Headers[HeaderName] = correlationId;

using var _ = _logger.BeginScope(new Dictionary<string, object?>
{
	["CorrelationId"] = correlationId,
	["RequestPath"] = context.Request.Path.Value,
});

await _next(context);
```

Then implement correlation id resolution:

```csharp
private static string ResolveCorrelationId(IHeaderDictionary headers)
{
	if (headers.TryGetValue(HeaderName, out StringValues values) && !StringValues.IsNullOrEmpty(values))
	{
		return values.ToString();
	}

	return Guid.NewGuid().ToString("N");
}
```

## Register Observability Services

Open `Program.cs` and add HTTP logging:

HTTP logs answer the operational questions first: which route was called, how long it took, and what status code came back. The correlation id makes those entries easy to join with deeper telemetry.

```csharp
builder.Services.AddHttpLogging(options =>
{
	options.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
	options.RequestHeaders.Add(CorrelationIdMiddleware.HeaderName);
	options.ResponseHeaders.Add(CorrelationIdMiddleware.HeaderName);
});
```

Register Application Insights:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITelemetryInitializer, DocumentApiTelemetryInitializer>();
builder.Services.AddApplicationInsightsTelemetry(options =>
{
	options.ConnectionString = applicationInsightsConnectionString;
	options.EnableAdaptiveSampling = applicationInsightsOptions.EnableAdaptiveSampling;
});
```

Then enable the middleware:

```csharp
app.UseHttpLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Emit Business Telemetry

Open `ApplicationInsightsDocumentActivityMonitor.cs` and implement search telemetry:

Framework telemetry tells you that a request happened. Business telemetry tells you what the request meant for the document workflow.

```csharp
_logger.LogInformation(
	"Document search completed. CacheHit={CacheHit} ResultCount={ResultCount}",
	cacheHit,
	resultCount);

_telemetryClient.TrackEvent(
	"Documents.Search.Completed",
	new Dictionary<string, string>
	{
		["CacheHit"] = cacheHit.ToString(),
		["HasQuery"] = (!string.IsNullOrWhiteSpace(criteria.Query)).ToString(),
	},
	new Dictionary<string, double>
	{
		["ResultCount"] = resultCount,
	});
```

Use the same pattern for upload and download:

```csharp
_telemetryClient.TrackEvent(
	"Documents.Upload.Completed",
	new Dictionary<string, string>
	{
		["DocumentId"] = document.Id,
		["ContentType"] = document.ContentType ?? string.Empty,
	},
	new Dictionary<string, double>
	{
		["SizeBytes"] = document.Size ?? 0,
		["DurationMs"] = durationMs,
	});
```

<div class="tip" data-title="Telemetry is useful when it is structured">

> Prefer named properties like `DocumentId`, `ContentType`, `DurationMs`, and `CacheHit` over long free-text messages. They are easier to query later.

</div>

## Build And Try It

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

Send a request with a correlation id:

```txt
X-Correlation-Id: workshop-correlation-id
```

<div class="task" data-title="Validation">

> Confirm that the response includes the same `X-Correlation-Id` value.
>
> If Application Insights is configured, run a document workflow and inspect the emitted custom events and metrics.

</div>
