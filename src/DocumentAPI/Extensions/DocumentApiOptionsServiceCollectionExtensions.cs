namespace DocumentAPI.Extensions;

using DocumentAPI.Options;

/// <summary>
/// Registers strongly typed options for the Document API and validates critical configuration at startup.
/// </summary>
public static class DocumentApiOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates document API options.
    /// </summary>
    public static IServiceCollection AddDocumentApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DocumentApiOptions.SectionName);

        services.Configure<DocumentApiOptions>(section);

        services
            .AddOptions<AuthenticationOptions>()
            .Bind(section.GetSection(nameof(DocumentApiOptions.Authentication)))
            .ValidateDataAnnotations()
            .Validate(options => options.SigningKey.Length >= 32, "DocumentApi:Authentication:SigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services
            .AddOptions<DocumentStorageOptions>()
            .Bind(section.GetSection(nameof(DocumentApiOptions.Storage)))
            .ValidateDataAnnotations()
            .Validate(options => IsAbsoluteHttpsUri(options.ServiceUri), "DocumentApi:Storage:ServiceUri must be a valid absolute https URI.")
            .ValidateOnStart();

        services
            .AddOptions<DocumentDatabaseOptions>()
            .Bind(section.GetSection(nameof(DocumentApiOptions.Database)))
            .ValidateDataAnnotations()
            .Validate(options => IsAbsoluteHttpsUri(options.ServiceUri), "DocumentApi:Database:ServiceUri must be a valid absolute https URI.")
            .Validate(options => !options.DatabaseName.Contains('/'), "DocumentApi:Database:DatabaseName must be a single name without '/'.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsAbsoluteHttpsUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
