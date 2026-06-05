namespace DocumentAPI.Services;

using DocumentAPI.Options;
using DocumentAPI.Persistence;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Health;
using DocumentAPI.Services.Identity;
using DocumentAPI.Services.Monitoring;
using DocumentAPI.Services.Storage;
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

        services.AddSingleton<IDocumentStorage>(serviceProvider => options.Storage.Provider switch
        {
            DocumentStorageProvider.LocalFile => ActivatorUtilities.CreateInstance<LocalFileDocumentStorage>(serviceProvider),
            DocumentStorageProvider.AzureBlob => ActivatorUtilities.CreateInstance<AzureBlobDocumentStorage>(serviceProvider),
            _ => throw new InvalidOperationException($"Unsupported storage provider '{options.Storage.Provider}'."),
        });

        services.AddSingleton<IDocumentActivityMonitor, ApplicationInsightsDocumentActivityMonitor>();

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
        if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
        {
            throw new InvalidOperationException("DocumentApi:Database:ConnectionString must be configured.");
        }

        var credential = AzureIdentityCredentialFactory.Create(databaseOptions.ManagedIdentityClientId);
        builder
            .UseSqlServer(NormalizeSqlConnectionString(databaseOptions.ConnectionString))
            .AddInterceptors(new AzureSqlAuthenticationInterceptor(credential));
    }

    /// <summary>
    /// Removes any embedded SQL credentials so the Microsoft Entra ID access token can be applied by the interceptor.
    /// </summary>
    private static string NormalizeSqlConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(builder.UserID) || !string.IsNullOrWhiteSpace(builder.Password))
        {
            throw new InvalidOperationException("DocumentApi:Database:ConnectionString must not contain SQL credentials. Use Microsoft Entra ID or a managed identity instead.");
        }

        builder.Remove("User ID");
        builder.Remove("Password");
        builder.Remove("Authentication");

        return builder.ConnectionString;
    }
}
