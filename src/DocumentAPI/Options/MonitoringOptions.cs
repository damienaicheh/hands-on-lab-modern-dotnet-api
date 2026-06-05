namespace DocumentAPI.Options;

/// <summary>
/// Groups monitoring-related settings for the API runtime.
/// </summary>
public sealed class MonitoringOptions
{
    /// <summary>
    /// Gets or sets the Application Insights monitoring settings.
    /// </summary>
    public ApplicationInsightsMonitoringOptions ApplicationInsights { get; set; } = new();
}