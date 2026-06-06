namespace DocumentAPI.Tests;

using System.Net;

/// <summary>
/// Verifies that Application Insights can be enabled without breaking host startup.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ApplicationInsightsRegistrationTests
{
    private readonly SqlServerFixture _sqlServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInsightsRegistrationTests" /> class.
    /// </summary>
    /// <param name="sqlServer">The shared SQL Server container fixture.</param>
    public ApplicationInsightsRegistrationTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    /// <summary>
    /// Verifies that the host starts successfully when Application Insights is enabled.
    /// </summary>
    [Fact]
    public async Task ApplicationInsightsCanBeEnabledWithoutBreakingStartup()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["DocumentApi:Monitoring:ApplicationInsights:Enabled"] = "true",
            ["DocumentApi:Monitoring:ApplicationInsights:ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope-0.in.applicationinsights.azure.com/",
        };

        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, configuration);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health?api-version=v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}