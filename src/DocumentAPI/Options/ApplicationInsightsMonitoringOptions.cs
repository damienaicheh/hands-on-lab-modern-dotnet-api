namespace DocumentAPI.Options;

/// <summary>
/// Defines how Application Insights should be configured for the API.
/// </summary>
public sealed class ApplicationInsightsMonitoringOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Application Insights telemetry is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Application Insights connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether adaptive sampling should be enabled.
    /// </summary>
    public bool EnableAdaptiveSampling { get; set; }

    /// <summary>
    /// Gets or sets the cloud role name that should be attached to emitted telemetry.
    /// </summary>
    public string CloudRoleName { get; set; } = "DocumentAPI";
}