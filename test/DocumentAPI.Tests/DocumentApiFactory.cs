namespace DocumentAPI.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Hosts the Document API in memory for integration testing against a SQL Server container.
/// </summary>
public sealed class DocumentApiFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "DocumentAPI";
    private const string Audience = "DocumentAPIClient";
    private const string SigningKey = "document-api-signing-key-to-randomly-generate";

    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
    private readonly string _databaseConnectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentApiFactory" /> class.
    /// </summary>
    /// <param name="sqlServerConnectionString">The connection string of the shared SQL Server container.</param>
    /// <param name="configurationOverrides">Optional configuration overrides applied to the in-memory test host.</param>
    public DocumentApiFactory(string sqlServerConnectionString, IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        _databaseConnectionString = new SqlConnectionStringBuilder(sqlServerConnectionString)
        {
            InitialCatalog = $"DocumentApi_{Guid.NewGuid():N}",
        }.ConnectionString;
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
    }

    /// <summary>
    /// Creates a valid bearer token for integration tests.
    /// </summary>
    /// <returns>A JWT bearer token accepted by the test host.</returns>
    public string CreateBearerToken()
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "integration-test-user")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["DocumentApi:Authentication:Issuer"] = Issuer,
                ["DocumentApi:Authentication:Audience"] = Audience,
                ["DocumentApi:Authentication:SigningKey"] = SigningKey,
                ["DocumentApi:ApplicationInsights:Enabled"] = "false",
                ["DocumentApi:Upload:MaxFileSizeBytes"] = "10485760",
                ["DocumentApi:Storage:ServiceUri"] = "https://tests.blob.core.windows.net/",
                ["DocumentApi:Storage:ContainerName"] = "documents",
                ["DocumentApi:Database:ServiceUri"] = "https://tests.database.windows.net",
                ["DocumentApi:Database:DatabaseName"] = "DocumentApiTests",
                ["DocumentApi:Search:CacheTtlSeconds"] = "60",
            };

            foreach (var configurationOverride in _configurationOverrides)
            {
                settings[configurationOverride.Key] = configurationOverride.Value;
            }

            configurationBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            var descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<DocumentDbContext>)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(DocumentDbContext)
                    || descriptor.ServiceType == typeof(IDocumentStorageService)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DocumentDbContext>(options => options.UseSqlServer(_databaseConnectionString));
            services.AddSingleton<IDocumentStorageService, InMemoryDocumentStorage>();
        });
    }
}