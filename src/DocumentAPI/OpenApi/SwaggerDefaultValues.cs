namespace DocumentAPI.OpenApi;

using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Applies API explorer metadata defaults that Swashbuckle does not infer automatically.
/// </summary>
public sealed class SwaggerDefaultValues : IOperationFilter
{
    /// <summary>
    /// Enriches operation metadata with API versioning defaults.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        operation.Deprecated |= apiDescription.ActionDescriptor.EndpointMetadata
            .OfType<ObsoleteAttribute>()
            .Any();

        var responses = operation.Responses;

        if (responses is not null)
        {
            foreach (var responseType in apiDescription.SupportedResponseTypes)
            {
                var responseKey = responseType.IsDefaultResponse
                    ? "default"
                    : responseType.StatusCode.ToString();

                if (!responses.TryGetValue(responseKey, out var response) || response is null || response.Content is null)
                {
                    continue;
                }

                var mediaTypes = responseType.ApiResponseFormats
                    .Select(format => format.MediaType)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var unsupportedContentTypes = response.Content.Keys
                    .Where(contentType => !mediaTypes.Contains(contentType))
                    .ToArray();

                foreach (var unsupportedContentType in unsupportedContentTypes)
                {
                    response.Content.Remove(unsupportedContentType);
                }
            }
        }

        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions
                .FirstOrDefault(item => item.Name == parameter.Name);

            if (description is null)
            {
                continue;
            }

            if (parameter is OpenApiParameter openApiParameter)
            {
                openApiParameter.Description ??= description.ModelMetadata?.Description;
                openApiParameter.Required |= description.IsRequired;
            }
        }
    }
}
