namespace DocumentAPI.Tests;

using DocumentAPI.DTOs;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Monitoring;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies the custom Application Insights business telemetry emitted for document operations.
/// </summary>
public sealed class ApplicationInsightsDocumentActivityMonitorTests
{
    /// <summary>
    /// Verifies that document business events and metrics are emitted to Application Insights.
    /// </summary>
    [Fact]
    public void Tracks_business_events_and_metrics()
    {
        var channel = new RecordingTelemetryChannel();

        using var configuration = new TelemetryConfiguration
        {
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope-0.in.applicationinsights.azure.com/",
            TelemetryChannel = channel,
        };

        var telemetryClient = new TelemetryClient(configuration);
        var activityMonitor = new ApplicationInsightsDocumentActivityMonitor(
            telemetryClient,
            NullLogger<ApplicationInsightsDocumentActivityMonitor>.Instance);

        activityMonitor.TrackUploadSucceeded(
            new DocumentDto
            {
                Id = "doc-123",
                FileName = "notes.txt",
                ContentType = "text/plain",
                Size = 11,
            },
            durationMs: 42);

        activityMonitor.TrackSearch(
            new DocumentSearchCriteria("workshop", null, null, "text/plain"),
            resultCount: 1,
            cacheHit: false,
            durationMs: 8);

        activityMonitor.TrackDownloadSucceeded(
            documentId: "doc-123",
            contentType: "text/plain",
            sizeBytes: 11,
            durationMs: 5);

        Assert.Contains(channel.TelemetryItems.OfType<EventTelemetry>(), item =>
            item.Name == "Documents.Upload.Completed" && item.Properties["DocumentId"] == "doc-123");
        Assert.Contains(channel.TelemetryItems.OfType<EventTelemetry>(), item =>
            item.Name == "Documents.Search.Completed" && item.Properties["CacheHit"] == "False");
        Assert.Contains(channel.TelemetryItems.OfType<EventTelemetry>(), item =>
            item.Name == "Documents.Download.Completed" && item.Properties["DocumentId"] == "doc-123");
        Assert.Contains(channel.TelemetryItems.OfType<MetricTelemetry>(), item => item.Name == "Documents.Upload.SizeBytes");
        Assert.Contains(channel.TelemetryItems.OfType<MetricTelemetry>(), item => item.Name == "Documents.Search.ResultCount");
        Assert.Contains(channel.TelemetryItems.OfType<MetricTelemetry>(), item => item.Name == "Documents.Download.DurationMs");
    }

    /// <summary>
    /// Captures telemetry emitted by the Application Insights client during tests.
    /// </summary>
    private sealed class RecordingTelemetryChannel : ITelemetryChannel
    {
        public List<ITelemetry> TelemetryItems { get; } = [];

        public bool? DeveloperMode { get; set; }

        public string? EndpointAddress { get; set; }

        public void Send(ITelemetry item)
        {
            TelemetryItems.Add(item);
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }
    }
}