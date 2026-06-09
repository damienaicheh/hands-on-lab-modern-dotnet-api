# Lab 10 - API Versioning

Your API now has real behavior. In this lab, you will introduce explicit API versioning so future changes can evolve without surprising clients.

The version will be read from the query string using `api-version=1.0`.

## What You Will Learn

In this lab, you will:

- Register API versioning services.
- Configure query string version reading.
- Create a versioned endpoint group for `/documents`.
- Keep `/health` outside the versioned group.
- Generate Swagger documents per API version.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Swagger versioning helpers are already provided in the `OpenApi` folder.

## Register Versioning

Open `Program.cs` and add API versioning services:

```csharp
builder.Services
	.AddApiVersioning(options =>
	{
		options.AssumeDefaultVersionWhenUnspecified = false;
		options.ReportApiVersions = true;
		options.ApiVersionReader = new QueryStringApiVersionReader("api-version");
	})
	.AddApiExplorer(options =>
	{
		options.GroupNameFormat = "'v'V";
	});
```

Then register the Swagger configuration helpers:

```csharp
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
```

Inside `AddSwaggerGen`, add the operation filter:

```csharp
options.OperationFilter<SwaggerDefaultValues>();
```

## Create A Versioned Documents Group

Open `DocumentEndpoints.cs` and replace the simple route group with a versioned API builder:

```csharp
var documentGroup = endpoints.NewVersionedApi("Documents");
var v1Group = documentGroup.MapGroup("/documents")
	.WithTags("Documents")
	.HasApiVersion(new ApiVersion(1));
```

All document endpoints mapped on `v1Group` now require a supported API version.

<div class="tip" data-title="Health stays public">

> Do not move `/health` into this versioned group. Health checks are operational endpoints and should remain easy for monitors to call.

</div>

## Expose Swagger Per Version

Still in `Program.cs`, resolve the version provider in the development block:

```csharp
var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
```

Then configure Swagger UI endpoints:

```csharp
app.UseSwaggerUI(options =>
{
	foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
	{
		options.SwaggerEndpoint(
			$"/swagger/{description.GroupName}/swagger.json",
			$"DocumentAPI {description.GroupName.ToUpperInvariant()}");
	}

	options.RoutePrefix = "swagger";
});
```

## Build And Try It

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

Call a versioned endpoint:

```txt
/documents/search?api-version=1.0
```

Then try the same endpoint without `api-version`.

<div class="task" data-title="Validation">

> Document endpoints should require `api-version=1.0`.
>
> `/health` should still be callable without an API version.

</div>

---