# Lab 1 - Setup Swagger in your API

Welcome to the first lab of the Document API workshop. In this step, you will expose an OpenAPI document for the Minimal API and make the first endpoints visible in Swagger UI.

The goal is intentionally small: understand where the API is wired, then add just enough metadata so the application becomes easy to discover and test.

You are not building business behavior yet. You are preparing the API surface so every next lab can be tested from a browser and described by an OpenAPI contract.

## What You Will Learn

In this lab, you will:

- Explore the `DocumentAPI` Minimal API entry point.
- Prepare the local application settings from the provided template.
- Register the ASP.NET Core endpoint explorer and Swagger generator.
- Enable Swagger UI in the development environment.
- Add the first metadata on document endpoints.
- Verify that the OpenAPI document is generated.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/appsettings.json`
- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

The health endpoint, DTOs, packages, and starter tests are already provided.

<div class="tip" data-title="Keep the scope small">

> This lab is about API discoverability. Do not implement upload, search, download, SQL, or storage yet. Those parts are intentionally incomplete in the starter.

</div>

## Prepare Local Settings

Before changing the API code, inside `src/DocumentAPI/` duplicate the `appsettings.json.template` file and rename the copy `appsettings.json`. This will create your local settings file from the provided template. The API reads `appsettings.json` when it starts, and the template contains placeholders for the Azure resources deployed in the prerequisites.

From the repository root, run:

Open `src/DocumentAPI/appsettings.json` and replace the placeholder values as described below:

```json
{
	"DocumentApi": {
		// ...
		"ApplicationInsights": {
			// ...
			"ConnectionString": "<Your Application Insights Connection String>"
			// ...
		},
		// ...
		"Storage": {
			// ...
			"ServiceUri": "https://<YOUR STORAGE ACCOUNT NAME>.blob.core.windows.net/",
			"ContainerName": "documents"
			// ...
		},
		"Database": {
			"ServiceUri": "https://<YOUR SQL SERVER NAME>.database.windows.net",
			"DatabaseName": "DocumentDb"
		}
		// ...
	}
}
```

Use the Azure resources created during the prerequisite setup:

- `DocumentApi:ApplicationInsights:ConnectionString`: in the Azure portal, open the Application Insights resource named `appi-...`, then copy **Connection String** from the **Overview** page.
- `DocumentApi:Storage:ServiceUri`: in the Azure portal, inside your resource group, copy the name of the Storage Account named `st...`, and update the placeholder. It should look like `https://<storage-account-name>.blob.core.windows.net/`.
- `DocumentApi:Database:ServiceUri`: in the Azure portal, inside your resource group, copy the name of the SQL Server resource named `sql-...`, and update the placeholder. It should look like `https://<server-name>.database.windows.net`.

Keep the rest of the settings as they are. You will use them in later labs when you implement the database and storage behavior.

## Register Swagger Services

Open `src/DocumentAPI/Program.cs` and find the Lab 1 TODO around Swagger services.

Minimal APIs do not use controllers, so Swagger needs the endpoint explorer to discover route handlers and their metadata. Think of this as the bridge between your route declarations and the generated OpenAPI contract.

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

Still in `Program.cs`, find the Lab 1 TODO inside the `IsDevelopment()` block and add:

```csharp
app.UseSwagger();
app.UseSwaggerUI();
```

This enables two things: the raw Swagger JSON document and the interactive Swagger UI page. Keeping it in the development block avoids exposing the UI by accident in another environment (in production typically).

Swagger UI stays development-only so the runtime pipeline remains clean outside local development.

## Add Endpoint Metadata

Open `DocumentEndpoints.cs` and enrich the document routes with OpenAPI metadata:

The metadata you add here is not only for the Swagger UI page. It's all the endpoints that will be visible in the generated OpenAPI document, which can be used by any OpenAPI consumer, such as Postman, code generators, or API gateways.

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

Build the project using the **Run** button in your IDE or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

Open Swagger UI at:

```txt
https://localhost:<port>/swagger
```

You must see something like this, with the three document endpoints visible:

![Swagger UI with three document endpoints visible](./assets/swagger-ui.png)

<div class="task" data-title="Validation">

> Confirm that Swagger UI opens and shows the document endpoints.
>
> The handlers can still throw `NotImplementedException`; that is expected at this stage.

</div>

---