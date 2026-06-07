namespace DocumentAPI.Endpoints;

using Azure;
using System.Text.Json;
using DocumentAPI.DTOs;
using DocumentAPI.Services.Documents;
using DocumentAPI.Services.Documents.Exceptions;
using DocumentAPI.Services.Storage;
using DocumentAPI.Services.Validators.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", UploadAsync)
            .WithName("Documents_upload")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id}/content", DownloadAsync)
            .WithName("Documents_download")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DocumentEndpoints");
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.Problem(versionError);
        }

        try
        {
            var documents = await documentService.SearchAsync(
                new DocumentSearchCriteria(query, title, tag, contentType),
                cancellationToken);

            return Results.Ok(documents);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Document search failed because the database dependency is unavailable.");
            return Results.Problem(
                detail: "The document database is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException exception)
        {
            logger.LogError(exception, "Document search failed due to a dependency timeout.");
            return Results.Problem(
                detail: "A downstream dependency timed out while processing the request.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Document search failed due to an unexpected error.");
            return Results.Problem(
                detail: "An unexpected error occurred while processing the document request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Uploads a document and its metadata using a multipart form request.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        [FromQuery(Name = ApiVersionValidation.ParameterName)] string? apiVersion,
        IDocumentUploadValidator validator,
        IDocumentService documentService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DocumentEndpoints");
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.Problem(versionError);
        }

        if (!request.HasFormContentType)
        {
            return Results.Problem(
                detail: "The request must use multipart/form-data.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        var metadataResult = TryReadMetadata(form["metadata"]);

        if (metadataResult.Error is not null)
        {
            return Results.Problem(metadataResult.Error);
        }

        var validationFailure = validator.Validate(file, metadataResult.Metadata);

        if (validationFailure is not null)
        {
            return Results.Problem(validationFailure.Problem);
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
        catch (DuplicateDocumentException exception)
        {
            logger.LogWarning(exception, "Duplicate document upload rejected.");
            return Results.Problem(
                detail: "A document with the same content already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (DocumentStorageIntegrityException exception)
        {
            logger.LogError(exception, "Document upload failed because storage integrity verification failed.");
            return Results.Problem(
                detail: "The document storage service reported a content integrity failure.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Document upload failed because the storage dependency is unavailable. StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode}",
                exception.Status,
                exception.ErrorCode);
            return Results.Problem(
                detail: "The document storage service is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Document upload failed because the database dependency is unavailable.");
            return Results.Problem(
                detail: "The document database is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException exception)
        {
            logger.LogError(exception, "Document upload failed due to a dependency timeout.");
            return Results.Problem(
                detail: "A downstream dependency timed out while processing the request.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Document upload failed due to an unexpected error.");
            return Results.Problem(
                detail: "An unexpected error occurred while processing the document request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Downloads the binary content of a document by identifier.
    /// </summary>
    private static async Task<IResult> DownloadAsync(
        [FromQuery(Name = ApiVersionValidation.ParameterName)] string? apiVersion,
        string id,
        IDocumentService documentService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DocumentEndpoints");
        var versionError = ApiVersionValidation.Validate(apiVersion);

        if (versionError is not null)
        {
            return Results.Problem(versionError);
        }

        try
        {
            var document = await documentService.DownloadAsync(id, cancellationToken);

            if (document is null)
            {
                return Results.Problem(
                    detail: "The requested document was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Document download failed because the storage dependency is unavailable. StorageStatus={StorageStatus} StorageErrorCode={StorageErrorCode}",
                exception.Status,
                exception.ErrorCode);
            return Results.Problem(
                detail: "The document storage service is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Document download failed because the database dependency is unavailable.");
            return Results.Problem(
                detail: "The document database is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TimeoutException exception)
        {
            logger.LogError(exception, "Document download failed due to a dependency timeout.");
            return Results.Problem(
                detail: "A downstream dependency timed out while processing the request.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Document download failed due to an unexpected error.");
            return Results.Problem(
                detail: "An unexpected error occurred while processing the document request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Parses the JSON metadata part from a multipart form request.
    /// </summary>
    private static (DocumentMetadataDto? Metadata, ProblemDetails? Error) TryReadMetadata(StringValues rawMetadata)
    {
        if (StringValues.IsNullOrEmpty(rawMetadata))
        {
            return (null, new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "The metadata part is required.",
            });
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<DocumentMetadataDto>(rawMetadata.ToString(), MetadataSerializerOptions);

            if (metadata is null)
            {
                return (null, new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = "The metadata part is required.",
                });
            }

            return (metadata, null);
        }
        catch (JsonException)
        {
            return (null, new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "The metadata part must contain valid JSON.",
            });
        }
    }
}
