namespace DocumentAPI.Tests;

using System.Net;
using System.Text.Json;
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
    /// Verifies that versioned document endpoints declare api-version as a required query parameter.
    /// </summary>
    [Fact]
    public async Task SwaggerJsonDocumentsEndpointsRequireApiVersionQueryParameter()
    {
        using var _ = ApplyAuthenticationEnvironmentVariables();
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString, Environments.Development);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        AssertApiVersionQueryParameter(document, "/documents/search", "get");
        AssertApiVersionQueryParameter(document, "/documents", "post");
        AssertApiVersionQueryParameter(document, "/documents/{id}/content", "get");
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

    private static void AssertApiVersionQueryParameter(JsonDocument document, string path, string verb)
    {
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(verb)
            .GetProperty("parameters");

        var apiVersionParameter = parameters.EnumerateArray()
            .Single(parameter =>
                parameter.GetProperty("name").GetString() == "api-version"
                && parameter.GetProperty("in").GetString() == "query");

        Assert.True(apiVersionParameter.GetProperty("required").GetBoolean());
    }
}
