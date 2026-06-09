# Lab 1 - Skeleton + Swagger

Welcome to the first lab of the Document API workshop. In this step, you will expose an OpenAPI document for the Minimal API and make the first endpoints visible in Swagger UI.

The goal is intentionally small: understand where the API is wired, then add just enough metadata so the application becomes easy to discover and test.

## What You Will Learn

In this lab, you will:

- Explore the `DocumentAPI` Minimal API entry point.
- Register the ASP.NET Core endpoint explorer and Swagger generator.
- Enable Swagger UI in the development environment.
- Add the first metadata on document endpoints.
- Verify that the OpenAPI document is generated.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

The health endpoint, DTOs, packages, and starter tests are already provided.

<div class="tip" data-title="Keep the scope small">

> This lab is about API discoverability. Do not implement upload, search, download, SQL, or storage yet. Those parts are intentionally incomplete in the starter.

</div>

## Register Swagger Services

Open `Program.cs` and find the Lab 1 TODO around Swagger services.

Replace it with:

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);

	if (File.Exists(xmlPath))
	{
		options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
	}
});
```

`AddEndpointsApiExplorer` lets Minimal API endpoints appear in OpenAPI. `AddSwaggerGen` creates the Swagger document.

## Enable Swagger UI

Still in `Program.cs`, find the Lab 1 TODO inside the development block and add:

```csharp
app.UseSwagger();
app.UseSwaggerUI();
```

Swagger UI stays development-only so the runtime pipeline remains clean outside local development.

## Add Endpoint Metadata

Open `DocumentEndpoints.cs` and enrich the document routes with OpenAPI metadata:

```csharp
v1Group.MapGet("/search", SearchAsync)
	.WithName("Documents_search")
	.Produces<IReadOnlyList<DocumentDto>>(StatusCodes.Status200OK)
	.ProducesProblem(StatusCodes.Status400BadRequest)
	.ProducesProblem(StatusCodes.Status500InternalServerError);

v1Group.MapPost("/", UploadAsync)
	.WithName("Documents_upload")
	.Accepts<IFormFile>("multipart/form-data")
	.Produces<DocumentDto>(StatusCodes.Status201Created)
	.ProducesProblem(StatusCodes.Status400BadRequest);

v1Group.MapGet("/{id}/content", DownloadAsync)
	.WithName("Documents_download")
	.Produces(StatusCodes.Status200OK)
	.ProducesProblem(StatusCodes.Status404NotFound);
```

You will add more response codes later as the API becomes more complete.

## Run The API

Build the project:

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

Then run it:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open Swagger UI at:

```txt
https://localhost:<port>/swagger
```

<div class="task" data-title="Validation">

> Confirm that Swagger UI opens and shows the document endpoints.
>
> The handlers can still throw `NotImplementedException`; that is expected at this stage.

</div>

---