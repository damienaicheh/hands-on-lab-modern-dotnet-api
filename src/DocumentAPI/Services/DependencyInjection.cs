namespace DocumentAPI.Services;

using Azure.Identity;
using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Health;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
using DocumentAPI.Services.Validators.Documents;
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
        services.AddDbContext<DocumentDbContext>(builder => ConfigureDatabase(builder, options.Database));

        services.AddSingleton<IDocumentStorage, AzureBlobDocumentStorage>();

        services.AddSingleton<IDocumentActivityMonitor, ApplicationInsightsDocumentActivityMonitor>();

        services.AddSingleton(DocumentResiliencePipeline.Create());
        services.AddSingleton<DocumentSearchCacheVersion>();
        services.AddTransient<IDocumentUploadValidator, DocumentUploadValidator>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IHealthStatusService, DocumentHealthStatusService>();

        return services;
    }

    /// <summary>
    /// Initializes the document database by applying any pending SQL Server migrations.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public static async Task InitializeDocumentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Configures the SQL Server provider for the document context.
    /// </summary>
    private static void ConfigureDatabase(DbContextOptionsBuilder builder, DocumentDatabaseOptions databaseOptions)
    {
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
    }

    /// <summary>
    /// Creates an Azure SQL connection string from the SQL server URI and database name.
    /// </summary>
    private static string CreateSqlConnectionStringFromSettings(string serviceUri, string databaseName)
    {
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
    }
}
