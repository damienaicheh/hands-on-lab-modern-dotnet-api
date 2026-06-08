namespace DocumentAPI.OpenApi;

using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Configures a Swagger document for each discovered API version.
/// </summary>
public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    /// <summary>
    /// Registers one Swagger document per API version.
    /// </summary>
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                new OpenApiInfo
                {
                    Title = "DocumentAPI",
                    Version = description.ApiVersion.ToString(),
                    Description = "Document management API built with .NET 10 Minimal APIs.",
                });
        }
    }
}
