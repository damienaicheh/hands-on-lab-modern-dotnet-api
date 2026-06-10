# Lab 10 - API Versioning

Your API now has real behavior. In this lab, you will introduce explicit API versioning so future changes can evolve without surprising clients.

The version will be read from the query string using `api-version=1.0`.

The goal is not to create a second version yet. The goal is to make version selection explicit before the API grows further.

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

Versioning makes the contract explicit. Instead of guessing which behavior a client expects, the API requires the caller to say which version it is using. Search for `TODO Lab 10` to find the right place to add this code:

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
// After: builder.Services.AddEndpointsApiExplorer();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
```

Inside `AddSwaggerGen`, add the operation filter:

```csharp
options.OperationFilter<SwaggerDefaultValues>();
```

This will make sure the generated Swagger documents reflect the API versioning configuration, for instance by adding `api-version` as a required query parameter.

## Create A Versioned Documents Group

Grouping the document routes keeps versioning in one place. Future versions can add a new group without touching `/health` or unrelated operational endpoints.

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


Swagger should show the same versioned contract that clients use at runtime. When future versions appear, each one can have its own generated document.

Still in `Program.cs`, resolve the version provider in the `if (app.Environment.IsDevelopment())` block:

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

## Run And Test Versioning

Start the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open `src/http/requests.http` and make sure `@apiVersion` is set to `1`.

Call a versioned endpoint:

```txt
/documents/search?api-version=1.0
```

Then temporarily remove `?api-version={{apiVersion}}` from one document request and send it again, you will get
a 404 Not Found or change the value of the `apiVersion` parameter to `2` for instance and you will get a 400 Bad Request with a message about missing API version. This confirms that the API now requires explicit version selection.

<div class="task" data-title="Validation">

> Document endpoints should require `api-version=1.0` or `api-version=1`.
>
> `/health` should still be callable without an API version.

</div>

---