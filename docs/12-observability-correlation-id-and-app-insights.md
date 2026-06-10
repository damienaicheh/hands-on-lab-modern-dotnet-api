# Lab 12 - Observability: Correlation ID and Application Insights

In the final lab, you will make the API easier to troubleshoot. You will add request correlation, HTTP logging, Application Insights telemetry, and business-level document activity monitoring.

The goal is to understand what happened, where it happened, and which document operation was involved.

You are adding signals that help during debugging and production support. Logs explain the request path, correlation connects events together, and custom telemetry explains the document operation.

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

HTTP logs answer the operational questions first: which route was called, how long it took, and what status code came back. The correlation id makes those entries easy to join with deeper telemetry. Search for `TODO Lab 12: Register HTTP logging and include the correlation id header.` to find the right place to add this code:

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

Application Insights receives the platform telemetry, while the telemetry initializer enriches it with request context such as the correlation id. Just after `builder.Services.AddApplicationInsightsTelemetry();` Register Application Insights:

```csharp
var applicationInsightsOptions = documentApiOptions.ApplicationInsights;
var applicationInsightsConnectionString = applicationInsightsOptions.Enabled
	? ResolveApplicationInsightsConnectionString(builder.Configuration, applicationInsightsOptions)
	: null;

if (applicationInsightsOptions.Enabled && string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
	throw new InvalidOperationException("Application Insights is enabled but no connection string was configured.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITelemetryInitializer, DocumentApiTelemetryInitializer>();
builder.Services.AddApplicationInsightsTelemetry(options =>
{
	options.ConnectionString = applicationInsightsConnectionString;
	options.EnableAdaptiveSampling = applicationInsightsOptions.EnableAdaptiveSampling;
});
```

Then enable the middleware before `app.UseAuthentication();`:

```csharp
app.UseHttpLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Emit Business Telemetry

You can create custom events and metrics in Application Insights to understand the business operations happening in the API. If you remember from the previous labs, you use the `DocumentActivityMonitor` interface in the service methods to track document operations. The implementation of that interface is where you will emit the custom telemetry. Let's do it know.

Open `ApplicationInsightsDocumentActivityMonitor.cs` and implement the `TrackSearch` method:

```csharp
_logger.LogInformation(
	"Document search completed. CacheHit={CacheHit} ResultCount={ResultCount} HasQuery={HasQuery} HasTitleFilter={HasTitleFilter} HasTagFilter={HasTagFilter} HasContentTypeFilter={HasContentTypeFilter}",
	cacheHit,
	resultCount,
	!string.IsNullOrWhiteSpace(criteria.Query),
	!string.IsNullOrWhiteSpace(criteria.Title),
	!string.IsNullOrWhiteSpace(criteria.Tag),
	!string.IsNullOrWhiteSpace(criteria.ContentType));

_telemetryClient.TrackEvent(
	"Documents.Search.Completed",
	new Dictionary<string, string>
	{
		["CacheHit"] = cacheHit.ToString(),
		["HasQuery"] = (!string.IsNullOrWhiteSpace(criteria.Query)).ToString(),
		["HasTitleFilter"] = (!string.IsNullOrWhiteSpace(criteria.Title)).ToString(),
		["HasTagFilter"] = (!string.IsNullOrWhiteSpace(criteria.Tag)).ToString(),
		["HasContentTypeFilter"] = (!string.IsNullOrWhiteSpace(criteria.ContentType)).ToString(),
	},
	new Dictionary<string, double>
	{
		["ResultCount"] = resultCount,
	});
```

Same thing for the `TrackUploadSucceeded` method:

```csharp
Use the same pattern for upload and download:

```csharp
 _logger.LogInformation(
	"Document upload completed. DocumentId={DocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
	document.Id,
	document.ContentType,
	document.Size,
	durationMs);

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

_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.SizeBytes", document.Size ?? 0));
_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.DurationMs", durationMs));
```

For the `TrackUploadDuplicate` method:

```csharp
_logger.LogWarning(
		"Duplicate document upload rejected. ExistingDocumentId={ExistingDocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
		existingDocumentId,
		contentType,
		sizeBytes,
		durationMs);

_telemetryClient.TrackEvent(
	"Documents.Upload.Duplicate",
	new Dictionary<string, string>
	{
		["ExistingDocumentId"] = existingDocumentId,
		["ContentType"] = contentType,
	},
	new Dictionary<string, double>
	{
		["SizeBytes"] = sizeBytes,
		["DurationMs"] = durationMs,
	});

_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Upload.DuplicateCount", 1));
```

For the `TrackDownloadSucceeded` method:
```csharp
_logger.LogInformation(
	"Document download completed. DocumentId={DocumentId} ContentType={ContentType} SizeBytes={SizeBytes} DurationMs={DurationMs}",
	documentId,
	contentType,
	sizeBytes,
	durationMs);

_telemetryClient.TrackEvent(
	"Documents.Download.Completed",
	new Dictionary<string, string>
	{
		["DocumentId"] = documentId,
		["ContentType"] = contentType,
	},
	new Dictionary<string, double>
	{
		["SizeBytes"] = sizeBytes,
		["DurationMs"] = durationMs,
	});

_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.SizeBytes", sizeBytes));
_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.DurationMs", durationMs));
```

And for the `TrackDownloadNotFound` method:

```csharp
_logger.LogWarning(
	"Document download returned no content. DocumentId={DocumentId} DurationMs={DurationMs}",
	documentId,
	durationMs);

_telemetryClient.TrackEvent(
	"Documents.Download.NotFound",
	new Dictionary<string, string>
	{
		["DocumentId"] = documentId,
	},
	new Dictionary<string, double>
	{
		["DurationMs"] = durationMs,
	});

_telemetryClient.TrackMetric(new MetricTelemetry("Documents.Download.NotFoundCount", 1));
```

<div class="tip" data-title="Telemetry is useful when it is structured">

> Prefer named properties like `DocumentId`, `ContentType`, `DurationMs`, and `CacheHit` over long free-text messages. They are easier to query later.

</div>

## Run And Test Observability

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http` and send a request with a correlation id header:

```txt
X-Correlation-Id: workshop-correlation-id
```

<div class="task" data-title="Validation">

> Confirm that the response includes the same `X-Correlation-Id` value.
>
> If Application Insights is configured, run the upload, search, and download requests from `src/http/requests.http`, then inspect the emitted custom events and metrics.

</div>

Inside Application Insights, you can see multiple signals:

In the overview, you can see the requests, failures, server response time:

![Application Insights overview](./assets/app-insights-overview.png)

After a few calls, you will be able to see the Application Map with the API dependencies:

![Application Insights map](./assets/app-insights-map.png)

With your application running from your machine, open the Live Metrics section and get more real-time insights. You can see incoming requests, failed requests, and performance counters:

![Application Insights live metrics](./assets/app-insights-live-metrics.png)

Inside the Failure section, you can see the failed requests with their properties and traces:

![Application Insights failures](./assets/app-insights-failures.png)

If you click on a specific request, you will see the details of that request and the exception raised:

![Application Insights request details](./assets/app-insights-request-details.png)

Inside performance, you can see the performance of your dependencies and operations:

![Application Insights performance](./assets/app-insights-performance.png)

Finally, you can also query the custom events and metrics you emitted by going to the Logs section and running queries like this one to see the search events:

![Application Insights custom events](./assets/app-insights-custom-events.png)

Select **KQL Mode** in the top right corner of the query editor. Type your query as described below select it and click on the **Run** button.

For duplicate upload copy/paste the following query and run it:

```bash
customEvents
| where name == "Documents.Upload.Duplicate"
| order by timestamp desc
```

For message logger copy/paste the following query and run it:

```bash
traces
| where message has "Duplicate document upload rejected"
| project timestamp, severityLevel, message, CorrelationId=tostring(customDimensions.["X-Correlation-Id"]), ServiceName=tostring(customDimensions.ServiceName), operation_Id
| order by timestamp desc
```

For metrics copy/paste the following query and run it:

```bash
customMetrics
| where name == "Documents.Upload.DuplicateCount"
| summarize Total=sum(value) by bin(timestamp, 15m)
```

Feel free to explore the other custom events and metrics you emitted.

---
