namespace DocumentAPI.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Validates OpenAPI endpoint exposure by environment.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OpenApiExposureTests
{
    private readonly SqlServerFixture _sqlServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiExposureTests" /> class.
    /// </summary>
    /// <param name="sqlServer">The shared SQL Server container fixture.</param>
    public OpenApiExposureTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    /// <summary>
    /// Verifies that OpenAPI JSON is available in Development.
    /// </summary>
    [Fact]
    public async Task OpenApiJsonIsAvailableInDevelopment()
    {
        using var _ = ApplyAuthenticationEnvironmentVariables();
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, Environments.Development);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that OpenAPI JSON is not publicly exposed in Production.
    /// </summary>
    [Fact]
    public async Task OpenApiJsonIsNotAvailableInProduction()
    {
        using var _ = ApplyAuthenticationEnvironmentVariables();
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.GetAsync("/openapi/v1.json");

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
