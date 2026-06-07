namespace DocumentAPI.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentAPI.DTOs;
using DocumentAPI.Models;

/// <summary>
/// Covers end-to-end HTTP behavior for the Document API endpoints.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class DocumentApiEndpointsTests
{
    private readonly SqlServerFixture _sqlServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentApiEndpointsTests" /> class.
    /// </summary>
    /// <param name="sqlServer">The shared SQL Server container fixture.</param>
    public DocumentApiEndpointsTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    /// <summary>
    /// Verifies that the health endpoint is available without api-version.
    /// </summary>
    [Fact]
    public async Task HealthIsAvailableWithoutApiVersion()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthyOrDegradedStatus>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Status));
    }

    /// <summary>
    /// Verifies that document search requires bearer authentication.
    /// </summary>
    [Fact]
    public async Task SearchRequiresAuthentication()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/documents/search?api-version=v1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<UnauthorizedError>();

        Assert.NotNull(error);
        Assert.Equal("UNAUTHORIZED", error!.Code);
        Assert.Equal("Access is unauthorized.", error.Message);
    }

    /// <summary>
    /// Verifies that document search still validates api-version when the caller is authenticated.
    /// </summary>
    [Fact]
    public async Task SearchRequiresApiVersionWhenAuthenticated()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

        var response = await client.GetAsync("/documents/search?query=workshop");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        Assert.NotNull(error);
        Assert.Equal(400, error!.Status);
        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    /// <summary>
    /// Verifies that the health endpoint echoes the correlation identifier header.
    /// </summary>
    [Fact]
    public async Task HealthEchoesCorrelationIdHeader()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health?api-version=v1");
        request.Headers.Add("X-Correlation-Id", "workshop-correlation-id");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var headerValues));
        Assert.Equal("workshop-correlation-id", Assert.Single(headerValues));
    }

    /// <summary>
    /// Verifies the upload, search, and download flow for a document.
    /// </summary>
    [Fact]
    public async Task UploadSearchAndDownloadRoundTripDocument()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

        using var uploadContent = CreateMultipartForm(
            fileName: "notes.txt",
            contentType: "text/plain",
            body: "hello world",
            metadata: new DocumentMetadataDto
            {
                Title = "Workshop Notes",
                Description = "Minimal API lab",
                Tags = ["lab", "notes"],
                Source = "integration-test",
            });

        var uploadResponse = await client.PostAsync("/documents?api-version=v1", uploadContent);

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var createdDocument = await uploadResponse.Content.ReadFromJsonAsync<DocumentDto>();

        Assert.NotNull(createdDocument);
        Assert.False(string.IsNullOrWhiteSpace(createdDocument!.Id));
        Assert.Equal("notes.txt", createdDocument.FileName);

        var searchResponse = await client.GetAsync("/documents/search?api-version=v1&query=workshop");

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<DocumentDto>>();

        Assert.NotNull(searchResults);
        Assert.Contains(searchResults!, document => document.Id == createdDocument.Id);

        var downloadResponse = await client.GetAsync($"/documents/{createdDocument.Id}/content?api-version=v1");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("text/plain", downloadResponse.Content.Headers.ContentType?.MediaType);

        var downloadedBody = await downloadResponse.Content.ReadAsStringAsync();

        Assert.Equal("hello world", downloadedBody);
    }

    /// <summary>
    /// Verifies that upload requests without metadata are rejected.
    /// </summary>
    [Fact]
    public async Task UploadRequiresMetadataPart()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("hello world"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "notes.txt");

        var response = await client.PostAsync("/documents?api-version=v1", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.NotNull(error);
        Assert.Equal(400, error!.Code);
        Assert.Equal("The metadata part is required.", error.Message);
    }

    /// <summary>
    /// Verifies that uploading identical document content twice returns a conflict response.
    /// </summary>
    [Fact]
    public async Task UploadingSameContentTwiceReturnsConflict()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

        using var firstUpload = CreateMultipartForm(
            fileName: "notes.txt",
            contentType: "text/plain",
            body: "duplicate-content",
            metadata: new DocumentMetadataDto { Title = "First version" });

        using var secondUpload = CreateMultipartForm(
            fileName: "notes-copy.txt",
            contentType: "text/plain",
            body: "duplicate-content",
            metadata: new DocumentMetadataDto { Title = "Second version" });

        var firstResponse = await client.PostAsync("/documents?api-version=v1", firstUpload);
        var secondResponse = await client.PostAsync("/documents?api-version=v1", secondUpload);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        var error = await secondResponse.Content.ReadFromJsonAsync<ApiError>();

        Assert.NotNull(error);
        Assert.Equal(409, error!.Code);
    }

    /// <summary>
    /// Verifies that downloading an unknown document identifier returns not found.
    /// </summary>
    [Fact]
    public async Task DownloadReturnsNotFoundForUnknownDocumentId()
    {
        using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

        var response = await client.GetAsync("/documents/unknown-document/content?api-version=v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();

        Assert.NotNull(error);
        Assert.Equal(404, error!.Code);
        Assert.Equal("The requested document was not found.", error.Message);
    }

    /// <summary>
    /// Creates a multipart form payload for document upload tests.
    /// </summary>
    private static MultipartFormDataContent CreateMultipartForm(
        string fileName,
        string contentType,
        string body,
        DocumentMetadataDto metadata)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json");
        form.Add(metadataContent, "metadata");

        return form;
    }
}