namespace DocumentAPI.Services;

using Azure.Identity;
using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Documents.Contracts;
using DocumentAPI.Services.Health;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
using DocumentAPI.Validators.Documents;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers the document API services and persistence into the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the document services, storage, and database to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="options">The bound document API options.</param>
    /// <returns>The same service collection so that calls can be chained.</returns>
    public static IServiceCollection AddDocumentServices(this IServiceCollection services, DocumentApiOptions options)
    {
        // <lab id="2">
        //|        // TODO Lab 2: Register DocumentDbContext with the SQL Server provider.
        services.AddDbContext<DocumentDbContext>(builder => ConfigureDatabase(builder, options.Database));
        // </lab>

        // Singleton: shared app-wide infrastructure and cache invalidation state.
        // <lab id="3">
        //|        // TODO Lab 3: Register the Azure Blob Storage implementation.
        services.AddSingleton<IDocumentStorageService, AzureBlobDocumentStorageService>();
        // </lab>

        services.AddSingleton<IDocumentActivityMonitor, ApplicationInsightsDocumentActivityMonitor>();

        // <lab id="5">
        //|        // TODO Lab 5: Register the SQL resilience pipeline.
        services.AddSingleton(DocumentResiliencePipeline.Create());
        // </lab>
        // <lab id="7">
        //|        // TODO Lab 7: Register the shared search cache version.
        services.AddSingleton<DocumentSearchCacheVersion>();
        // </lab>

        // Transient: the validator is lightweight and stateless, so a fresh instance per resolution avoids carrying mutable state across calls.
        services.AddTransient<IDocumentUploadValidator, DocumentUploadValidator>();
        
        // Scoped: these services depend on a request-scoped DocumentDbContext, so sharing one instance per request keeps a consistent unit-of-work and avoids lifetime mismatches.
        // <lab id="7">
        //|        services.AddScoped<IDocumentService>(_ => throw new NotImplementedException("TODO Lab 4: Implement the document workflow before calling document endpoints."));
        services.AddScoped<IDocumentService, DocumentService>();
        // </lab>
        // <lab id="8">
        //|        services.AddScoped<IHealthStatusService>(_ => throw new NotImplementedException("TODO Lab 8: Implement health status checks before calling the health endpoint."));
        services.AddScoped<IHealthStatusService, DocumentHealthStatusService>();
        // </lab>

        return services;
    }

    /// <summary>
    /// Initializes the document database by applying any pending SQL Server migrations.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static async Task InitializeDocumentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // <lab id="2">
        //|    // TODO Lab 2: Apply pending EF Core migrations at startup.
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        // </lab>
    }

    /// <summary>
    /// Configures the SQL Server provider for the document context.
    /// </summary>
    private static void ConfigureDatabase(DbContextOptionsBuilder builder, DocumentDatabaseOptions databaseOptions)
    {
        // <lab id="2">
        //|    // TODO Lab 2: Configure the SQL Server provider.
        if (string.IsNullOrWhiteSpace(databaseOptions.ServiceUri))
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must be configured.");
        }

        if (string.IsNullOrWhiteSpace(databaseOptions.DatabaseName))
        {
            throw new InvalidOperationException("DocumentApi:Database:DatabaseName must be configured.");
        }

        var credential = new DefaultAzureCredential();
        builder
            .UseSqlServer(CreateSqlConnectionStringFromSettings(databaseOptions.ServiceUri, databaseOptions.DatabaseName))
            .AddInterceptors(new AzureSqlAuthenticationInterceptor(credential));
        // </lab>
    }

    /// <summary>
    /// Creates an Azure SQL connection string from the SQL server URI and database name.
    /// </summary>
    private static string CreateSqlConnectionStringFromSettings(string serviceUri, string databaseName)
    {
        // <lab id="2">
        //|    // TODO Lab 2: Build a SQL Server connection string from the configured URI and database name.
        //|    return string.Empty;
        if (!Uri.TryCreate(serviceUri, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must be a valid absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must use the https scheme.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must include a SQL Server host.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must not contain query string or fragment.");
        }

        if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException("DocumentApi:Database:ServiceUri must not include a database path. Configure the database in DocumentApi:Database:DatabaseName.");
        }

        if (databaseName.Contains('/'))
        {
            throw new InvalidOperationException("DocumentApi:Database:DatabaseName must be a single name without '/'.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = uri.IsDefaultPort ? uri.Host : $"{uri.Host},{uri.Port}",
            InitialCatalog = Uri.UnescapeDataString(databaseName),
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
        };

        return builder.ConnectionString;
        // </lab>
    }
}
