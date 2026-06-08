namespace DocumentAPI.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Validates Swagger endpoint exposure by environment.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SwaggerExposureTests
{
    private readonly SqlServerFixture _sqlServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaggerExposureTests" /> class.
    /// </summary>
    /// <param name="sqlServer">The shared SQL Server container fixture.</param>
    public SwaggerExposureTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    /// <summary>
    /// Verifies that Swagger JSON is available in Development.
    /// </summary>
    [Fact]
    public async Task SwaggerJsonIsAvailableInDevelopment()
    {
        using var _ = ApplyAuthenticationEnvironmentVariables();
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, Environments.Development);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that Swagger JSON is not publicly exposed in Production.
    /// </summary>
    [Fact]
    public async Task SwaggerJsonIsNotAvailableInProduction()
    {
        using var _ = ApplyAuthenticationEnvironmentVariables();
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static IDisposable ApplyAuthenticationEnvironmentVariables()
    {
        const string issuerKey = "DocumentApi__Authentication__Issuer";
        const string audienceKey = "DocumentApi__Authentication__Audience";
        const string signingKeyKey = "DocumentApi__Authentication__SigningKey";

        var previousIssuer = Environment.GetEnvironmentVariable(issuerKey);
        var previousAudience = Environment.GetEnvironmentVariable(audienceKey);
        var previousSigningKey = Environment.GetEnvironmentVariable(signingKeyKey);

        Environment.SetEnvironmentVariable(issuerKey, "DocumentAPI");
        Environment.SetEnvironmentVariable(audienceKey, "DocumentAPIClient");
        Environment.SetEnvironmentVariable(signingKeyKey, "document-api-signing-key-to-randomly-generate");

        return new DelegateDisposable(() =>
        {
            Environment.SetEnvironmentVariable(issuerKey, previousIssuer);
            Environment.SetEnvironmentVariable(audienceKey, previousAudience);
            Environment.SetEnvironmentVariable(signingKeyKey, previousSigningKey);
        });
    }

    private sealed class DelegateDisposable(Action disposeAction) : IDisposable
    {
        public void Dispose()
        {
            disposeAction();
        }
    }
}
