namespace DocumentAPI.Tests;

using DocumentAPI.Extensions;
using DocumentAPI.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Verifies startup validation for strongly typed document API options.
/// </summary>
public sealed class DocumentApiOptionsValidationTests
{
    [Fact]
    public async Task StartFailsWhenAuthenticationSigningKeyIsMissing()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["DocumentApi:Authentication:SigningKey"] = string.Empty,
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(exception.Failures, failure => failure.Contains("SigningKey", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartFailsWhenStorageServiceUriIsInvalid()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["DocumentApi:Storage:ServiceUri"] = "not-a-uri",
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(exception.Failures, failure => failure.Contains("ServiceUri", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartFailsWhenDatabaseNameIsMissing()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["DocumentApi:Database:DatabaseName"] = string.Empty,
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(exception.Failures, failure => failure.Contains("DatabaseName", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartSucceedsWhenCriticalOptionsAreValid()
    {
        using var host = BuildHost(new Dictionary<string, string?>());

        await host.StartAsync();
        await host.StopAsync();
    }

    private static IHost BuildHost(IReadOnlyDictionary<string, string?> overrides)
    {
        var builder = Host.CreateApplicationBuilder();
        var settings = new Dictionary<string, string?>
        {
            ["DocumentApi:Authentication:Issuer"] = "DocumentAPI",
            ["DocumentApi:Authentication:Audience"] = "DocumentAPIClient",
            ["DocumentApi:Authentication:SigningKey"] = "document-api-signing-key-to-randomly-generate",
            ["DocumentApi:Storage:ServiceUri"] = "https://tests.blob.core.windows.net/",
            ["DocumentApi:Storage:ContainerName"] = "documents",
            ["DocumentApi:Database:ServiceUri"] = "https://tests.database.windows.net",
            ["DocumentApi:Database:DatabaseName"] = "DocumentApiTests",
        };

        foreach (var overrideSetting in overrides)
        {
            settings[overrideSetting.Key] = overrideSetting.Value;
        }

        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddDocumentApiOptions(builder.Configuration);

        return builder.Build();
    }
}
