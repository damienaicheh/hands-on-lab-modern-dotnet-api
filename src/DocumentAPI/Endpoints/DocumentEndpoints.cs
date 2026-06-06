namespace DocumentAPI.Endpoints;

using System.Text.Json;
using DocumentAPI.DTOs;
using DocumentAPI.Models;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Documents.Exceptions;
using DocumentAPI.Services.Validators.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Registers the document-related Minimal API endpoints.
/// </summary>
public static class DocumentEndpoints
{
    private static readonly JsonSerializerOptions MetadataSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the document upload, search, and download endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The original endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        group.MapGet("/search", SearchAsync)
            .WithName("Documents_search")
            .Produces<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<UnauthorizedError>(StatusCodes.Status401Unauthorized);

        group.MapPost("/", UploadAsync)
            .WithName("Documents_upload")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentDto>(StatusCodes.Status201Created)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status409Conflict)
            .Produces<ApiError>(StatusCodes.Status413PayloadTooLarge)
            .Produces<UnauthorizedError>(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id}/content", DownloadAsync)
            .WithName("Documents_download")
            .Produces(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status404NotFound)
            .Produces<UnauthorizedError>(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    /// <summary>
    /// Searches documents using free-text and metadata filters.
    /// </summary>
    private static async Task<IResult> SearchAsync(
        [FromQuery(Name = ApiVersionValidation.ParameterName)] string? apiVersion,
        [FromQuery] string? query,
        [FromQuery] string? title,
        [FromQuery] string? tag,
        [FromQuery] string? contentType,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.BadRequest(versionError);
        }

        var documents = await documentService.SearchAsync(
            new DocumentSearchCriteria(query, title, tag, contentType),
            cancellationToken);

        return Results.Ok(documents);
    }

    /// <summary>
    /// Uploads a document and its metadata using a multipart form request.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        [FromQuery(Name = ApiVersionValidation.ParameterName)] string? apiVersion,
        IDocumentUploadValidator validator,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.BadRequest(versionError);
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new ApiError { Code = 400, Message = "The request must use multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var metadataResult = TryReadMetadata(form["metadata"]);

        if (metadataResult.Error is not null)
        {
            return Results.BadRequest(metadataResult.Error);
        }

        var validationFailure = validator.Validate(file, metadataResult.Metadata);

        if (validationFailure is not null)
        {
            return Results.Json(validationFailure.Error, statusCode: validationFailure.StatusCode);
        }

        await using var fileStream = file!.OpenReadStream();
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);

        try
        {
            var document = await documentService.UploadAsync(
                new DocumentUploadCommand(file.FileName, file.ContentType, buffer.ToArray(), metadataResult.Metadata!),
                cancellationToken);

            return Results.Json(document, statusCode: StatusCodes.Status201Created);
        }
        catch (DuplicateDocumentException)
        {
            return Results.Json(
                new ApiError
                {
                    Code = StatusCodes.Status409Conflict,
                    Message = "A document with the same content already exists.",
                },
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Downloads the binary content of a document by identifier.
    /// </summary>
    private static async Task<IResult> DownloadAsync(
        [FromQuery(Name = ApiVersionValidation.ParameterName)] string? apiVersion,
        string id,
        IDocumentService documentService,
        CancellationToken cancellationToken)
    {
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.BadRequest(versionError);
        }

        var document = await documentService.DownloadAsync(id, cancellationToken);

        if (document is null)
        {
            return Results.NotFound(new ApiError { Code = 404, Message = "The requested document was not found." });
        }

        return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Parses the JSON metadata part from a multipart form request.
    /// </summary>
    private static (DocumentMetadataDto? Metadata, ApiError? Error) TryReadMetadata(StringValues rawMetadata)
    {
        if (StringValues.IsNullOrEmpty(rawMetadata))
        {
            return (null, new ApiError { Code = 400, Message = "The metadata part is required." });
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<DocumentMetadataDto>(rawMetadata.ToString(), MetadataSerializerOptions);

            if (metadata is null)
            {
                return (null, new ApiError { Code = 400, Message = "The metadata part is required." });
            }

            return (metadata, null);
        }
        catch (JsonException)
        {
            return (null, new ApiError { Code = 400, Message = "The metadata part must contain valid JSON." });
        }
    }
}