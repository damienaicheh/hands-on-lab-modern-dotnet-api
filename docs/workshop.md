---
published: true
type: workshop
title: Product Hands-on Lab - Modern .NET API
short_title: Modern .NET API
description: This workshop will teach you how to build a modern .NET API. You will implement a document management API that allows users to upload, search, and download documents while following best practices for API design, security, and observability.
level: beginner # Required. Can be 'beginner', 'intermediate' or 'advanced'
navigation_numbering: false
authors: # Required. You can add as many authors as needed
  - Damien Aicheh
contacts: # Required. Must match the number of authors
  - "@damienaicheh"
duration_minutes: 360
tags: asp net, minimal API, .NET 10, azure, storage account, application insights, SQL, devcontainer, csu
navigation_levels: 3
banner_url: assets/banner.jpg
audience: developers

---

# Product Hands-on Lab - Modern .NET API

Welcome to this hands-on lab! In this workshop, you will learn how to build a modern .NET API using ASP.NET Core Minimal APIs, Azure SQL Database, Azure Blob Storage, and Application Insights. You will implement a document management API that allows users to upload, search, and download documents while following best practices for API design, security, and observability.

---

## Prerequisites

Before starting this lab, be sure to set your Azure environment :

- An Azure Subscription with the **Contributor** role to create and manage the labs' resources and deploy the infrastructure as code
- Register the Azure providers on your Azure Subscription if not done yet: `Microsoft.Storage`, `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights`.

To retrieve the lab content :

- A GitHub account (Free, Team or Enterprise)
- Create a [fork][repo-fork] of the repository from the **main** branch to help you keep track of your changes

### Setup your local environment

The following tools and access will be necessary to run the lab on a local environment:  

- [Git client][git-client]
- [Visual Studio][visual-studio] installed
- [Azure CLI][az-cli-install] installed on your machine
- [Terraform][download-terraform] installed on your machine (to deploy the infrastructure as code)
- [Docker Desktop][docker-desktop] installed on your machine

Once you have set up your local environment, you can clone the repository you just forked on your machine, and open the solution in Visual Studio.

### Sign in to Azure

> - Log into your Azure subscription in your environment using Azure CLI and on the [Azure Portal][az-portal] using your credentials.
> - Register the Azure providers on your Azure Subscription if not done yet: `Microsoft.Storage`, `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights`.

```bash
# Login to Azure : 
# --tenant : Optional | In case your Azure account has access to multiple tenants

# Option 1 : Local Environment 
az login --tenant <yourtenantid or domain.com>
# Option 2 : Github Codespace : you might need to specify --use-device-code parameter to ease the az cli authentication process
az login --use-device-code --tenant <yourtenantid or domain.com>

# Display your account details
az account show
# Select your Azure subscription
az account set --subscription <subscription-id>

# Register the following Azure providers if they are not already
# Azure Storage
az provider register --namespace 'Microsoft.Storage'
# Azure SQL
az provider register --namespace 'Microsoft.Sql'
# Azure Monitor
az provider register --namespace 'Microsoft.Insights'
# Azure Log Analytics
az provider register --namespace 'Microsoft.OperationalInsights'
```

### Deploy the infrastructure

First, you need to initialize the terraform infrastructure by running the following command:

```bash
# Run the following line which will dynamically set the subscription ID as an environment variable:
# On Bash or Zsh:
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
# On Windows PowerShell:
$env:ARM_SUBSCRIPTION_ID = (az account show --query id -o tsv)

# Initialize terraform
cd infra && terraform init
```

Then run the following command to deploy the infrastructure:

```bash
# Apply the deployment directly
terraform apply -auto-approve
```

The deployment should take around 5 minutes to complete.

[az-cli-install]: https://learn.microsoft.com/en-us/cli/azure/install-azure-cli
[az-portal]: https://portal.azure.com
[visual-studio]: https://visualstudio.microsoft.com/
[vs-code]: https://code.visualstudio.com/
[docker-desktop]: https://www.docker.com/products/docker-desktop/
[repo-fork]: https://github.com/damienaicheh/hands-on-lab-agent-framework-on-azure/fork
[git-client]: https://git-scm.com/downloads
[github-account]: https://github.com/join
[download-terraform]: https://developer.hashicorp.com/terraform/install

---

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

Before changing the API code, copy the `appsettings.json.template` content into the `appsettings.json` file. This will create your local settings file from the provided template. The API reads `appsettings.json` when it starts, and the template contains placeholders for the Azure resources deployed in the prerequisites.

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

# Lab 2 - SQL Database and Dependency Injection

In this lab, you will add SQL Server persistence for document metadata. The API will still not upload documents end-to-end, but the persistence layer will be ready for the next labs.

The starter already provides the `Document` entity, database options, Entity Framework Core mapping, migration files, and Azure SQL authentication helper. Your job is to connect those pieces through `DocumentDbContext` and dependency injection.

At the end of this lab, the API will know how to talk to the database, even if no endpoint is using it fully yet.

## What You Will Learn

In this lab, you will:

- Expose a `DbSet<Document>` from the Entity Framework Core context.
- Apply entity configurations from the current assembly.
- Register `DocumentDbContext` in the application container.
- Initialise the database when the application starts.
- Build a SQL Server connection string from strongly typed options.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Persistence/DocumentDbContext.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

The entity, options, mappings, and migration are already provided.

## Complete The DbContext

Open `DocumentDbContext.cs` and expose the document metadata set:

The `DbContext` is the unit of work for Entity Framework Core. It is the object your services will use to query and save document metadata without writing SQL by hand. For this lab you only need to expose a `DbSet<Document>`, which represents the table of documents in the database. Each `Document` instance corresponds to a row in that table.

```csharp
public DbSet<Document> Documents => Set<Document>();
```

You can check the `Document` class inside the `Entities` folder.

Then apply the Entity Framework Core configurations by overriding `OnModelCreating` in this class:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
	base.OnModelCreating(modelBuilder);
	modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
}
```

This keeps table mapping, indexes, and column details in `DocumentConfiguration.cs`.

## Register SQL Server

Open `DependencyInjection.cs` and register the context inside `AddDocumentServices`:

Registering the context in dependency injection lets services ask for `DocumentDbContext` through their constructor. ASP.NET Core then creates it with the right lifetime for each request.

```csharp
services.AddDbContext<DocumentDbContext>(builder => ConfigureDatabase(builder, options.Database));
```

As you can see the database configuration is reading the appsettings through `DocumentApiOptions`, which contains all the configuration for the API. We point to the `Database` section of the configuration, which you filled in the previous lab.

Then implement startup migration:

```csharp
public static async Task InitializeDocumentDatabaseAsync(
	this IServiceProvider services,
	CancellationToken cancellationToken = default)
{
	using var scope = services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();

	await dbContext.Database.MigrateAsync(cancellationToken);
}
```

<div class="tip" data-title="Why migrations at startup?">

> For this hands-on lab, applying migrations at startup keeps the environment simple. In production, database changes can be deployed using the API code or custom scripts outside of the application. Both approaches are valid, and the best choice depends on your operational practices and risk management.

</div>

## Configure The SQL Provider

The workshop uses identity-based access to all services. That means the application receives a token through the `DefaultAzureCredential` class from the Azure Identity library instead of storing a SQL username and password in configuration.

Add the provider configuration:

```csharp
private static void ConfigureDatabase(DbContextOptionsBuilder builder, DocumentDatabaseOptions databaseOptions)
{
	if (string.IsNullOrWhiteSpace(databaseOptions.ServiceUri))
	{
		throw new InvalidOperationException("DocumentApi:Database:ServiceUri must be configured.");
	}

	if (string.IsNullOrWhiteSpace(databaseOptions.DatabaseName))
	{
		throw new InvalidOperationException("DocumentApi:Database:DatabaseName must be configured.");
	}

	var credential = new DefaultAzureCredential();
	builder
		.UseSqlServer(CreateSqlConnectionStringFromSettings(databaseOptions.ServiceUri, databaseOptions.DatabaseName))
		.AddInterceptors(new AzureSqlAuthenticationInterceptor(credential));
}
```

As you can see, the connection string is built from the configured service URI and database name. The `AzureSqlAuthenticationInterceptor` takes care of requesting a token for the database on every connection attempt.

Now add the helper that converts the configured server URI into a SQL connection string:

```csharp
private static string CreateSqlConnectionStringFromSettings(string serviceUri, string databaseName)
{
	var uri = new Uri(serviceUri, UriKind.Absolute);
	var builder = new SqlConnectionStringBuilder
	{
		DataSource = uri.IsDefaultPort ? uri.Host : $"{uri.Host},{uri.Port}",
		InitialCatalog = Uri.UnescapeDataString(databaseName),
		Encrypt = true,
		TrustServerCertificate = false,
		ConnectTimeout = 30,
	};

	return builder.ConnectionString;
}
```

## Build The Project

Build the project using the **Run** button in your Visual Studio or the following command lines:

```bash
dotnet run --project src/DocumentAPI/DocumentAPI.csproj
```

**After** the webbrowser opens, go to your Azure resource group and open your Database named `DocumentDb` in Azure and check the "Query editor (preview)" blade. You should see the `Documents` table there, which means the API successfully applied the migration at startup.

![Azure SQL Database with the Documents table visible in the query editor](./assets/azure-sql-documents-table.png)

and also the migration history table with the initial migration applied:

![Azure SQL Database with the migration history table showing the initial migration applied](./assets/azure-sql-migration-history.png)

<div class="task" data-title="Validation">

> The project should build successfully.
>
> You now have SQL metadata persistence registered, even though upload is not complete yet.

</div>

---

# Lab 3 - Blob Storage Integration

In the previous lab, you wired SQL Server to store the file metadata of your documents. Now you will add Azure Blob Storage for the binary content of uploaded documents.

The API keeps metadata and content separated: SQL Server stores searchable properties, while Blob Storage stores the file bytes.

This separation is common in document systems: the database is great for filters and relationships, while storage accounts are built for durable file content.

## What You Will Learn

In this lab, you will:

- Instantiate a `BlobServiceClient` from the Azure SDK with `DefaultAzureCredential`.
- Resolve the configured blob container.
- Implement the storage service methods to:
	- Save content to Blob Storage.
	- Open document content for download.
	- Delete content when cleanup is required.
- Register the storage service in dependency injection.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Storage/AzureBlobDocumentStorageService.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

The storage interface, options, packages, and configuration keys are already provided.

## Create The Blob Client

Open `AzureBlobDocumentStorageService.cs` and implement the constructor:

Blob Storage is the right place for the binary file content because it is optimized for streams and large objects. SQL Server stays focused on metadata that you need to query. The file uploaded will be stored as a blob.

```csharp
public AzureBlobDocumentStorageService(IOptions<DocumentApiOptions> options)
{
	var storageOptions = options.Value.Storage;
	var credential = new DefaultAzureCredential();
	var blobServiceClient = new BlobServiceClient(new Uri(storageOptions.ServiceUri), credential);
	_containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.ContainerName);
}
```

As you can see, the Azure SDK client is straightforward to instantiate. The `ServiceUri` and `ContainerName` come from configuration, and the credential uses `DefaultAzureCredential`, which is a great option for local development and Azure-hosted environments. Locally, it can use your Azure CLI sign-in, and in Azure, it can use a managed identity if configured.

## Save Content

The storage service receives a stream, not a byte array. This keeps the API friendly to larger files because callers do not need to load everything into memory before saving.

Implement `SaveAsync`:

```csharp
public async Task SaveAsync(string contentHash, Stream content, byte[] md5Hash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);

	var blobClient = _containerClient.GetBlobClient(contentHash);
	await blobClient.UploadAsync(content, cancellationToken);
}
```

The blob name uses the content hash. This makes duplicate content easier to detect in later labs. As you can see the Azure SDK makes it easy to upload a stream with `UploadAsync`.

## Open And Delete Content

Deletion is used later when the upload workflow needs to roll back a blob after a database failure. Keeping it in the storage abstraction makes the business service easier to read.

Implement `DeleteAsync`:

```csharp
public async Task DeleteAsync(string contentHash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);
	await _containerClient.DeleteBlobIfExistsAsync(
		contentHash,
		DeleteSnapshotsOption.IncludeSnapshots,
		cancellationToken: cancellationToken);
}
```

Then implement `OpenReadAsync`:

Returning `null` for a missing blob keeps the service contract simple. The document service can then decide whether that becomes a `404` or another recovery path.

```csharp
public async Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken)
{
	await EnsureInitializedAsync(cancellationToken);

	try
	{
		return await _containerClient.GetBlobClient(contentHash).OpenReadAsync(cancellationToken: cancellationToken);
	}
	catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
	{
		return null;
	}
}
```

## Register The Storage Service

Open `DependencyInjection.cs` and after the `DocumentDbContext` registration, register the Azure Service implementation:

```csharp
services.AddSingleton<IDocumentStorageService, AzureBlobDocumentStorageService>();
```

<div class="tip" data-title="Why singleton?">

> Azure SDK clients are thread-safe and designed to be reused. A singleton avoids recreating clients for every request.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> The project should build successfully.
>
> Upload is still incomplete, but the API now has a storage implementation ready for the next lab.

</div>

---

# Lab 4 - Upload Happy Path

You now have metadata persistence and blob storage. In this lab, you will connect them through the first real document workflow: upload a valid multipart request, save the file, persist metadata, and return `201 Created`.

This lab focuses on the happy path. Robust validation and dependency failure handling come next.

The goal is to see the full route from HTTP request to database row and blob content. Once that path exists, it becomes much easier to harden it.

## What You Will Learn

In this lab, you will:

- Read a multipart form request from a Minimal API endpoint.
- Deserialize the metadata JSON part.
- Call the document service from the endpoint.
- Save file content to Blob Storage.
- Save document metadata to SQL Server.
- Return a `DocumentDto` response.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`

Contracts, DTOs, storage, and database services are already provided.

## Implement The Upload Endpoint

Open `DocumentEndpoints.cs` and find the `UploadAsync` handler.

The endpoint should stay thin: it understands HTTP, form data, and response codes. The service will own the actual document workflow.

Read the form data:

```csharp
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
```

Then call the service:

The endpoint passes a command object to the service instead of many separate parameters. That makes the upload intent explicit and keeps the method signature readable.

```csharp
var validationFailure = validator.Validate(file, metadataResult.Metadata);

if (validationFailure is not null)
{
	return Results.Problem(validationFailure.Problem);
}

await using var fileStream = file!.OpenReadStream();

var document = await documentService.UploadAsync(
	new DocumentUploadCommand(file.FileName, file.ContentType, fileStream, file.Length, metadataResult.Metadata!),
	cancellationToken);

return Results.Json(document, statusCode: StatusCodes.Status201Created);
```

## Implement The Service Happy Path

Open `DocumentService.cs` and implement `UploadAsync`.

This is where the application switches from HTTP concerns to business concerns: compute an identity for the content, store the bytes, store the metadata, and return the public DTO.

Compute the content hash and reset the stream:

```csharp
if (!command.Content.CanSeek)
{
	throw new ArgumentException("The upload content stream must support seeking.", nameof(command));
}

var stopwatch = Stopwatch.StartNew();
var md5 = command.Content.ComputeMd5();
var hash = Convert.ToHexString(md5);
command.Content.Position = 0;
```

Save the blob and persist metadata:

```csharp
var documentId = Guid.NewGuid().ToString("N");

await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);

var document = new Document
{
	Id = documentId,
	FileName = command.FileName,
	ContentType = command.ContentType,
	Size = command.Length,
	ContentHash = hash,
	CreatedUtc = DateTimeOffset.UtcNow,
};

_dbContext.Documents.Add(document);
await _dbContext.SaveChangesAsync(cancellationToken);

var documentDto = ToDocumentDto(document);
stopwatch.Stop();
_activityMonitor.TrackUploadSucceeded(documentDto, stopwatch.Elapsed.TotalMilliseconds);
return documentDto;
```

Then implement the DTO mapping helper:

```csharp
private static DocumentDto ToDocumentDto(Document document)
{
	return new DocumentDto
	{
		Id = document.Id,
		FileName = document.FileName,
		ContentType = document.ContentType,
		Size = document.Size,
		Metadata = new DocumentMetadataDto
		{
			Title = document.Title,
			Description = document.Description,
			Source = document.Source,
			Tags = document.Tags.Count > 0 ? document.Tags : null,
		},
	};
}
```

## Test The Upload

Build the project:

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

You can use Swagger UI or `src/http/requests.http` to send a multipart upload.

<div class="task" data-title="Validation">

> A valid upload should return `201 Created` with a `DocumentDto` payload.
>
> Duplicate detection and advanced error handling are not implemented yet. That is the purpose of the next lab.

</div>

---

# Lab 5 - Upload Robustness

The upload happy path works, but real APIs need to be defensive. In this lab, you will reject invalid requests, detect duplicate content, clean up after dependency failures, and return predictable error responses.

This is the lab where the upload workflow becomes production-shaped.

You will keep the successful path from the previous lab, then add the defensive behavior around it: reject bad input early, avoid duplicate content, and clean up when one dependency succeeds but another fails.

## What You Will Learn

In this lab, you will:

- Validate multipart uploads.
- Reject empty files and unsupported content types.
- Detect duplicate documents using a content hash.
- Return `409 Conflict` for duplicate content.
- Clean up Blob Storage when SQL persistence fails.
- Use a resilience pipeline around database operations.
- Map dependency errors to clean HTTP responses.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Validators/Documents/DocumentUploadValidator.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Exceptions, upload options, content type helpers, and the resilience pipeline are already provided.

## Strengthen Upload Validation

Open `DocumentUploadValidator.cs` and implement the upload rules:

Validation is deliberately outside the endpoint body. That keeps HTTP parsing separate from business rules and makes the rules easier to test in isolation later.

```csharp
if (file is null)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The file part is required.",
	});
}

if (metadata is null)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The metadata part is required.",
	});
}
```

Then add size and content type checks:

```csharp
if (file.Length <= 0)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "The uploaded file cannot be empty.",
	});
}

if (file.Length > _options.Upload.MaxFileSizeBytes)
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status413PayloadTooLarge,
		Title = "Payload Too Large",
		Detail = $"The uploaded file exceeds the configured maximum size of {_options.Upload.MaxFileSizeBytes} bytes.",
	});
}

if (!DocumentContentTypes.IsSupported(file.ContentType))
{
	return new RequestValidationFailure(new ProblemDetails
	{
		Status = StatusCodes.Status400BadRequest,
		Title = "Bad Request",
		Detail = "Unsupported content type. Allowed values are PDF, TXT, DOC, and DOCX.",
	});
}
```

Return `null` when the request is valid.

This keeps the calling code straightforward: a validation failure contains a `ProblemDetails` response, and `null` means the request can continue.

## Detect Duplicates In The Service

Open `DocumentService.cs`. Before saving to storage, check whether a document with the same content hash already exists:

The content hash is a stable fingerprint of the file bytes. If two uploads have the same hash, the API can treat them as the same document content even if the file name is different.

```csharp
var existingDocument = await _resiliencePipeline.ExecuteAsync(
	async token => await _dbContext.Documents
		.AsNoTracking()
		.FirstOrDefaultAsync(document => document.ContentHash == hash, token),
	cancellationToken);

if (existingDocument is not null)
{
	stopwatch.Stop();
	_activityMonitor.TrackUploadDuplicate(
		existingDocument.Id,
		command.ContentType,
		command.Length,
		stopwatch.Elapsed.TotalMilliseconds);
	throw new DuplicateDocumentException(existingDocument.Id);
}
```

Wrap the storage and database writes in a `try` block and track whether the blob was uploaded:

```csharp
var documentId = Guid.NewGuid().ToString("N");
var blobUploaded = false;

try
{
	await _storage.SaveAsync(hash, command.Content, md5, cancellationToken);
	blobUploaded = true;

	// Create the entity, add it to the DbContext, then save with the resilience pipeline.
}
catch (DbUpdateException exception)
{
	if (blobUploaded)
	{
		await _storage.DeleteAsync(hash, cancellationToken);
	}

	_logger.LogError(exception, "Document upload failed due to a database error. ContentHash={ContentHash}", hash);
	throw;
}
```

<div class="tip" data-title="Duplicate races">

> The final solution rechecks for a conflicting content hash when SQL fails. That avoids deleting a blob that belongs to another request that uploaded the same content at the same time.

</div>

## Map Errors At The Endpoint

Open `DocumentEndpoints.cs` and wrap the service call:

The service throws domain or dependency exceptions. The endpoint translates those exceptions into HTTP responses that clients can understand and handle consistently.

```csharp
try
{
	var document = await documentService.UploadAsync(
		new DocumentUploadCommand(file.FileName, file.ContentType, fileStream, file.Length, metadataResult.Metadata!),
		cancellationToken);

	return Results.Json(document, statusCode: StatusCodes.Status201Created);
}
catch (DuplicateDocumentException)
{
	return Results.Problem(
		detail: "A document with the same content already exists.",
		statusCode: StatusCodes.Status409Conflict);
}
catch (DbUpdateException)
{
	return Results.Problem(
		detail: "The document database is temporarily unavailable.",
		statusCode: StatusCodes.Status503ServiceUnavailable);
}
```

Add storage and unexpected error mappings using the same pattern.

The important idea is consistency. Clients should not need to know whether the failure came from SQL Server, Blob Storage, or the document workflow internals.

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Try these scenarios from Swagger UI or the HTTP file:
>
> - missing metadata returns `400 Bad Request`
> - empty file returns `400 Bad Request`
> - unsupported content type returns `400 Bad Request`
> - duplicate content returns `409 Conflict`

</div>

---

# Lab 6 - Download and Search Functionality

The API can now upload documents reliably. In this lab, you will let clients retrieve stored content and search document metadata.

Search and download complete the core document workflow.

After this lab, a document can go through the complete lifecycle from upload to retrieval. The API becomes useful enough to validate with real end-to-end scenarios.

## What You Will Learn

In this lab, you will:

- Query document metadata by id.
- Open document content from Blob Storage.
- Return `404 Not Found` when metadata or content is missing.
- Search documents with optional filters.
- Expose download and search endpoints.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Documents/DocumentService.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

The search criteria and download result contracts are already provided.

## Implement Download In The Service

Open `DocumentService.cs` and implement `DownloadAsync`:

Download needs both dependencies: SQL tells you which blob to read and what content type to return, while Blob Storage provides the actual stream.

```csharp
var document = await _resiliencePipeline.ExecuteAsync(
	async token => await _dbContext.Documents
		.AsNoTracking()
		.FirstOrDefaultAsync(candidate => candidate.Id == id, token),
	cancellationToken);

if (document is null)
{
	_activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
	return null;
}

var stream = await _storage.OpenReadAsync(document.ContentHash, cancellationToken);

if (stream is null)
{
	_activityMonitor.TrackDownloadNotFound(id, stopwatch.Elapsed.TotalMilliseconds);
	return null;
}

_activityMonitor.TrackDownloadSucceeded(document.Id, document.ContentType, document.Size, stopwatch.Elapsed.TotalMilliseconds);
return new DocumentContentResult(document.FileName, document.ContentType, stream);
```

## Implement Search In The Service

Implement the metadata query in `QueryDocumentsAsync`:

The query starts with filters that SQL Server can handle efficiently. After loading the narrowed set, the service applies tag and free-text checks in memory to keep the lab code approachable.

```csharp
var query = _dbContext.Documents.AsNoTracking();

if (!string.IsNullOrWhiteSpace(criteria.Title))
{
	query = query.Where(document => document.Title == criteria.Title);
}

if (!string.IsNullOrWhiteSpace(criteria.ContentType))
{
	query = query.Where(document => document.ContentType == criteria.ContentType);
}

var documents = await query
	.OrderByDescending(document => document.CreatedUtc)
	.ToListAsync(cancellationToken);
```

Then apply filters that are easier to evaluate in memory:

```csharp
IEnumerable<Document> filtered = documents;

if (!string.IsNullOrWhiteSpace(criteria.Tag))
{
	filtered = filtered.Where(document =>
		document.Tags.Any(tag => string.Equals(tag, criteria.Tag, StringComparison.OrdinalIgnoreCase)));
}

if (!string.IsNullOrWhiteSpace(criteria.Query))
{
	filtered = filtered.Where(document => ContainsFreeText(document, criteria.Query));
}

return filtered.Select(ToDocumentDto).ToArray();
```

## Expose The Endpoints

Open `DocumentEndpoints.cs` and implement the search handler:

The endpoint only builds a `DocumentSearchCriteria` object from query string values. This keeps filtering rules inside the service instead of spreading them through the HTTP layer.

```csharp
var documents = await documentService.SearchAsync(
	new DocumentSearchCriteria(query, title, tag, contentType),
	cancellationToken);

return Results.Ok(documents);
```

Then implement download:

`Results.File` streams the content back to the caller and keeps the original file name and content type. Range processing is enabled so clients can resume or partially read supported downloads.

```csharp
var document = await documentService.DownloadAsync(id, cancellationToken);

if (document is null)
{
	return Results.Problem(
		detail: "The requested document was not found.",
		statusCode: StatusCodes.Status404NotFound);
}

return Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
```

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Upload a document, search for it, then download its content.
>
> Also try downloading an unknown id and confirm that the API returns `404 Not Found`.

</div>

---

# Lab 7 - Search Caching

Search is often called repeatedly with the same filters. In this lab, you will add in-memory caching to reduce repeated database work while keeping the API contract unchanged.

The important part is not just caching; it is caching safely and invalidating results when new documents are uploaded.

The API response should not change when caching is added. You are improving performance behind the same contract, which is a useful pattern for production APIs.

## What You Will Learn

In this lab, you will:

- Register `IMemoryCache`.
- Create a deterministic cache key from search criteria.
- Cache search results with a configurable TTL.
- Track cache hit and cache miss behavior.
- Invalidate search results after upload.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Services/Documents/DocumentService.cs`

The cache options and shared cache version service are already provided.

## Register Memory Cache

Open `Program.cs` and register the memory cache:

This adds the built-in in-memory cache service to the application container. It is enough for a single API instance and keeps the lab focused on caching behavior.

```csharp
builder.Services.AddMemoryCache();
```

## Add Cache Around Search

Open `DocumentService.cs` and update `SearchAsync`.

Caching belongs around the service query, not inside the endpoint. This way every caller benefits from the same behavior, even if another endpoint or background process reuses the service later.

Create the key and check the cache:

```csharp
var cacheKey = CreateCacheKey(_cacheVersion.Current, criteria);

var cacheHit = _cache.TryGetValue(cacheKey, out IReadOnlyList<DocumentDto>? cachedDocuments)
	&& cachedDocuments is not null;
IReadOnlyList<DocumentDto> documents;

if (cacheHit)
{
	documents = cachedDocuments!;
}
else
{
	documents = await _resiliencePipeline.ExecuteAsync(
		async token => await QueryDocumentsAsync(criteria, token),
		cancellationToken);

	_cache.Set(
		cacheKey,
		documents,
		new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.Search.CacheTtlSeconds)),
		});
}

_activityMonitor.TrackSearch(criteria, documents.Count, cacheHit);
return documents;
```

## Create A Deterministic Cache Key

Add the helper methods:

The key must be deterministic: the same criteria should always produce the same key, even if the user adds extra spaces or changes casing.

```csharp
private static string CreateCacheKey(int cacheVersion, DocumentSearchCriteria criteria)
{
	return string.Join(
		"::",
		"documents-search",
		cacheVersion,
		NormalizeCacheSegment(criteria.Query),
		NormalizeCacheSegment(criteria.Title),
		NormalizeCacheSegment(criteria.Tag),
		NormalizeCacheSegment(criteria.ContentType));
}

private static string NormalizeCacheSegment(string? value)
{
	return string.IsNullOrWhiteSpace(value)
		? string.Empty
		: value.Trim().ToLowerInvariant();
}
```

The shared cache version is part of the key. Incrementing it invalidates all previous search entries without having to enumerate cache keys.

## Invalidate After Upload

After a successful upload and database save, increment the cache version:

```csharp
_cacheVersion.Increment();
```

<div class="tip" data-title="Why not remove cache entries one by one?">

> Search has many possible filter combinations. A versioned key is simpler and avoids tracking every possible cache key manually.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Run the same search twice and confirm that the second call uses the cached path.
>
> Upload a new document, search again, and confirm the cache is invalidated.

</div>

---

# Lab 8 - Health Endpoint

The API now depends on SQL Server and Blob Storage. In this lab, you will expose a health endpoint that reports whether those dependencies are reachable.

Health endpoints are used by humans, deployment systems, and monitoring tools. They should be simple, stable, and safe to call without authentication.

The endpoint is not meant to expose private diagnostics. It gives just enough information to know whether the API should receive traffic.

## What You Will Learn

In this lab, you will:

- Check database connectivity.
- Check Blob Storage connectivity.
- Re-evaluate dependency connectivity on a short cache interval instead of relying on a startup-only result.
- Return `Healthy`, `Degraded`, or `Unhealthy`.
- Include per-dependency details when the service is degraded.
- Keep `/health` anonymous.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Services/Health/DocumentHealthStatusService.cs`
- `src/DocumentAPI/Endpoints/HealthEndpoints.cs`

The health contracts, response models, and DI registration are already provided.

## Evaluate Dependency Health

Open `DocumentHealthStatusService.cs` and implement `GetStatusAsync`:

A health endpoint should check the dependencies that make the API useful. Here, the service is healthy only when both SQL metadata and Blob content access are available.

The service includes a small in-memory cache around connectivity probes. This keeps `/health` inexpensive while still refreshing the dependency state periodically when the endpoint is called.

```csharp
var storageHealthy = await GetCachedConnectivityAsync(
	StorageConnectivityCacheKey,
	token => _storage.CanConnectAsync(token),
	cancellationToken);
var databaseHealthy = await GetCachedConnectivityAsync(
	DatabaseConnectivityCacheKey,
	token => _dbContext.Database.CanConnectAsync(token),
	cancellationToken);
var checks = new Dictionary<string, HealthDependencyState>(StringComparer.Ordinal)
{
	["database"] = databaseHealthy
		? new HealthDependencyState(HealthStatus.Healthy)
		: new HealthDependencyState(HealthStatus.Unhealthy, "Database is unreachable."),
	["storage"] = storageHealthy
		? new HealthDependencyState(HealthStatus.Healthy)
		: new HealthDependencyState(HealthStatus.Unhealthy, "Storage is unreachable."),
};
```

Then return the overall state:

```csharp
if (storageHealthy && databaseHealthy)
{
	return new HealthStateResult(HealthStatus.Healthy, true, checks);
}

if (storageHealthy || databaseHealthy)
{
	return new HealthStateResult(HealthStatus.Degraded, true, checks);
}

return new HealthStateResult(HealthStatus.Unhealthy, false, checks);
```

## Map Health To HTTP

Open `HealthEndpoints.cs` and implement the response mapping:

The response has two layers: an HTTP status for infrastructure tools and a body that gives humans or dashboards more detail.

```csharp
var status = await healthStatusService.GetStatusAsync(cancellationToken);

if (!status.IsAvailable)
{
	return Results.Json(
		new UnhealthyStatus { Status = status.Status.ToString() },
		statusCode: StatusCodes.Status503ServiceUnavailable);
}

if (status.Status != HealthStatus.Degraded)
{
	return Results.Ok(new HealthyOrDegradedStatus { Status = status.Status.ToString() });
}
```

For degraded mode, include dependency details:

`Degraded` is useful when the service is still reachable but not fully healthy. It gives operators a clear signal without pretending everything is fine.

```csharp
return Results.Ok(new HealthyOrDegradedStatus
{
	Status = status.Status.ToString(),
	Checks = status.Checks.ToDictionary(
		pair => pair.Key,
		pair => new HealthCheckStatus
		{
			Status = pair.Value.Status.ToString(),
			Description = pair.Value.Description,
		},
		StringComparer.Ordinal),
});
```

<div class="tip" data-title="Why health is anonymous">

> Monitoring systems often call health endpoints without user credentials. Later, when JWT authentication is added, `/health` will remain public.

</div>

## Build The Project

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> Call `/health` and confirm that it returns a status value.
>
> If one dependency is unavailable, the response should be `Degraded` and include dependency details.

</div>

---

# Lab 9 - Unit Testing

You now have the main API behaviors in place. In this lab, you will add automated tests so the upload, search, download, duplicate detection, and edge cases can be validated repeatedly.

The test infrastructure is already prepared. Your focus is the test intent, not the boilerplate.

Each test should explain one behavior in code: what is arranged, what action happens, and what result proves the behavior is correct.

## What You Will Learn

In this lab, you will:

- Test `DocumentService` with EF Core InMemory.
- Use a fake document storage service.
- Verify upload happy path behavior.
- Verify duplicate detection.
- Verify missing and existing downloads.
- Cover endpoint behavior through the test factory.

## Files To Open

You only need to edit these files:

- `tests/DocumentAPI.Tests/DocumentServiceTests.cs`
- `tests/DocumentAPI.Tests/DocumentApiEndpointsTests.cs`

The factory, SQL Server fixture, fake storage, packages, and internal visibility setup are already provided.

## Test Upload Happy Path

Open `DocumentServiceTests.cs` and implement the upload success test:

This test stays close to the service boundary. It uses a real EF Core context, but replaces Blob Storage and telemetry with simple in-memory doubles.

```csharp
await using var dbContext = CreateDbContext();
var storage = new RecordingStorage();
var activityMonitor = new RecordingActivityMonitor();
var service = CreateService(dbContext, storage, activityMonitor);

var command = new DocumentUploadCommand(
	"notes.txt",
	"text/plain",
	new MemoryStream(Encoding.UTF8.GetBytes("hello world")),
	Encoding.UTF8.GetByteCount("hello world"),
	new DocumentMetadataDto
	{
		Title = "Workshop Notes",
		Description = "Minimal API lab",
		Source = "unit-test",
		Tags = ["lab", "notes"],
	});

var document = await service.UploadAsync(command, CancellationToken.None);

var persisted = await dbContext.Documents.AsNoTracking().SingleAsync();

Assert.Equal(document.Id, persisted.Id);
Assert.Equal("Workshop Notes", persisted.Title);
Assert.Equal(1, storage.SaveCallCount);
Assert.Single(activityMonitor.UploadSucceededDocuments);
```

## Test Duplicate Content

Add a document with the same hash, then upload the same bytes again:

This test protects the rule introduced in the robustness lab. If someone changes upload later, the test will catch accidental duplicate storage.

```csharp
var duplicateBytes = Encoding.UTF8.GetBytes("same-content");
dbContext.Documents.Add(new Document
{
	Id = "existing-doc",
	FileName = "existing.txt",
	ContentType = "text/plain",
	Size = duplicateBytes.Length,
	ContentHash = duplicateBytes.Md5ToHexString(),
	CreatedUtc = DateTimeOffset.UtcNow,
});
await dbContext.SaveChangesAsync();

var exception = await Assert.ThrowsAsync<DuplicateDocumentException>(
	() => service.UploadAsync(command, CancellationToken.None));

Assert.Equal("existing-doc", exception.ExistingDocumentId);
Assert.Equal(0, storage.SaveCallCount);
```

## Test Download Behavior

For a missing document:

Download has two important branches: the document exists or it does not. Testing both keeps the public `404` behavior reliable.

```csharp
var result = await service.DownloadAsync("missing", CancellationToken.None);

Assert.Null(result);
Assert.Single(activityMonitor.DownloadNotFoundDocumentIds);
```

For an existing document, seed storage and metadata, then read the returned stream:

```csharp
var content = Encoding.UTF8.GetBytes("stored-content");
var contentHash = content.Md5ToHexString();
storage.Seed(contentHash, content);

dbContext.Documents.Add(new Document
{
	Id = "doc-42",
	FileName = "doc-42.txt",
	ContentType = "text/plain",
	Size = content.Length,
	ContentHash = contentHash,
	CreatedUtc = DateTimeOffset.UtcNow,
});
await dbContext.SaveChangesAsync();

var result = await service.DownloadAsync("doc-42", CancellationToken.None);

Assert.NotNull(result);
```

## Test HTTP Endpoints

Open `DocumentApiEndpointsTests.cs` and add an integration test for the round trip:

Endpoint tests validate the wiring that unit tests cannot see: routing, multipart parsing, authentication headers, model serialization, and status codes.

```csharp
using var factory = new DocumentApiFactory(_sqlServer.ConnectionString);
using var client = factory.CreateClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateBearerToken());

using var uploadContent = CreateMultipartForm(
	fileName: "notes.txt",
	contentType: "text/plain",
	body: "hello world",
	metadata: new DocumentMetadataDto { Title = "Workshop Notes" });

var uploadResponse = await client.PostAsync("/documents?api-version=1.0", uploadContent);

Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
```

Use the same pattern for missing metadata, duplicate upload, and unknown download id.

<div class="tip" data-title="Use Copilot for edge cases">

> Once the first test passes, ask Copilot to suggest additional edge cases around content type, metadata parsing, duplicate upload, and missing storage content.

</div>

## Run The Tests

```bash
dotnet test tests/DocumentAPI.Tests/DocumentAPI.Tests.csproj
```

<div class="task" data-title="Validation">

> The test project should pass consistently.
>
> If this is the first run, SQL Server Testcontainers may take longer while the container image is prepared.

</div>

---

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

Versioning makes the contract explicit. Instead of guessing which behavior a client expects, the API requires the caller to say which version it is using.

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

Grouping the document routes keeps versioning in one place. Future versions can add a new group without touching `/health` or unrelated operational endpoints.

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

Swagger should show the same versioned contract that clients use at runtime. When future versions appear, each one can have its own generated document.

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

# Lab 11 - JWT Authentication

The document API now exposes useful operations. In this lab, you will protect those operations with JWT bearer authentication while keeping `/health` anonymous.

Authentication is configured from the options already provided in the starter.

You will protect the document workflow, not the whole application. Operational endpoints such as `/health` remain open so monitoring can keep working.

## What You Will Learn

In this lab, you will:

- Register JWT bearer authentication.
- Validate issuer, audience, signing key, and lifetime.
- Return a predictable `401 Unauthorized` response.
- Protect `/documents` endpoints.
- Keep Swagger usable with bearer tokens.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Endpoints/DocumentEndpoints.cs`

Authentication options, appsettings, and test token helpers are already provided.

## Register JWT Bearer Authentication

Open `Program.cs` and add JWT bearer authentication:

JWT bearer authentication lets the API validate a signed token without calling an external service for every request. The issuer, audience, and signing key define which tokens this API trusts.

```csharp
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.RequireHttpsMetadata = documentApiOptions.Authentication.RequireHttpsMetadata;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = documentApiOptions.Authentication.Issuer,
			ValidateAudience = true,
			ValidAudience = documentApiOptions.Authentication.Audience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(documentApiOptions.Authentication.SigningKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1),
		};
	});
```

Then enable the middleware before endpoint execution:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

## Return A Clean 401 Response

Inside `AddJwtBearer`, configure `OnChallenge`:

The default challenge response can vary depending on middleware behavior. Returning `ProblemDetails` gives clients a predictable JSON shape.

```csharp
options.Events = new JwtBearerEvents
{
	OnChallenge = async context =>
	{
		context.HandleResponse();
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		context.Response.ContentType = "application/problem+json";
		await context.Response.WriteAsJsonAsync(new ProblemDetails
		{
			Status = StatusCodes.Status401Unauthorized,
			Title = "Unauthorized",
			Detail = "Access is unauthorized.",
		});
	},
};
```

## Protect Document Endpoints

Open `DocumentEndpoints.cs` and require authorization on the documents group:

Authorization is applied at the route group level so every current and future `/documents` endpoint inherits the same protection by default.

```csharp
var v1Group = documentGroup.MapGroup("/documents")
	.WithTags("Documents")
	.RequireAuthorization()
	.HasApiVersion(new ApiVersion(1));
```

Do not add authorization to `/health`.

## Add Bearer Support To Swagger

Inside `AddSwaggerGen`, add a bearer security definition:

This does not authenticate anyone by itself. It only teaches Swagger UI how to send an `Authorization: Bearer ...` header when you test protected endpoints.

```csharp
var bearerSecurityScheme = new OpenApiSecurityScheme
{
	Name = "Authorization",
	Type = SecuritySchemeType.Http,
	Scheme = "bearer",
	BearerFormat = "JWT",
	In = ParameterLocation.Header,
	Description = "Provide a valid JWT bearer token.",
};

options.AddSecurityDefinition("Bearer", bearerSecurityScheme);
```

## Build And Try It

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

Call the search endpoint without a token:

```txt
/documents/search?api-version=1.0
```

It should return `401 Unauthorized`.

<div class="task" data-title="Validation">

> Confirm that `/documents` requires a token.
>
> Confirm that `/health` still works anonymously.

</div>

---

# Lab 12 - Observability: Correlation ID and Application Insights

In the final lab, you will make the API easier to troubleshoot. You will add request correlation, HTTP logging, Application Insights telemetry, and business-level document activity monitoring.

The goal is to understand what happened, where it happened, and which document operation was involved.

You are adding signals that help during debugging and production support. Logs explain the request path, correlation connects events together, and custom telemetry explains the document operation.

## What You Will Learn

In this lab, you will:

- Read or generate an `X-Correlation-Id` header.
- Echo the correlation id in the response.
- Add the correlation id to logging scope and telemetry.
- Register Application Insights.
- Emit custom events and metrics for upload, search, and download.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Observability/CorrelationIdMiddleware.cs`
- `src/DocumentAPI/Program.cs`
- `src/DocumentAPI/Services/Monitoring/ApplicationInsightsDocumentActivityMonitor.cs`

The telemetry initializer, monitoring options, and monitor interface are already provided.

## Add Correlation ID Middleware

Open `CorrelationIdMiddleware.cs` and implement `InvokeAsync`:

A correlation id is the thread you can follow through logs, HTTP responses, and telemetry. If the caller already sends one, the API keeps it; otherwise it creates one.

```csharp
var correlationId = ResolveCorrelationId(context.Request.Headers);
context.TraceIdentifier = correlationId;
context.Response.Headers[HeaderName] = correlationId;

using var _ = _logger.BeginScope(new Dictionary<string, object?>
{
	["CorrelationId"] = correlationId,
	["RequestPath"] = context.Request.Path.Value,
});

await _next(context);
```

Then implement correlation id resolution:

```csharp
private static string ResolveCorrelationId(IHeaderDictionary headers)
{
	if (headers.TryGetValue(HeaderName, out StringValues values) && !StringValues.IsNullOrEmpty(values))
	{
		return values.ToString();
	}

	return Guid.NewGuid().ToString("N");
}
```

## Register Observability Services

Open `Program.cs` and add HTTP logging:

HTTP logs answer the operational questions first: which route was called, how long it took, and what status code came back. The correlation id makes those entries easy to join with deeper telemetry.

```csharp
builder.Services.AddHttpLogging(options =>
{
	options.LoggingFields = HttpLoggingFields.RequestMethod
		| HttpLoggingFields.RequestPath
		| HttpLoggingFields.ResponseStatusCode
		| HttpLoggingFields.Duration;
	options.RequestHeaders.Add(CorrelationIdMiddleware.HeaderName);
	options.ResponseHeaders.Add(CorrelationIdMiddleware.HeaderName);
});
```

Register Application Insights:

Application Insights receives the platform telemetry, while the telemetry initializer enriches it with request context such as the correlation id.

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITelemetryInitializer, DocumentApiTelemetryInitializer>();
builder.Services.AddApplicationInsightsTelemetry(options =>
{
	options.ConnectionString = applicationInsightsConnectionString;
	options.EnableAdaptiveSampling = applicationInsightsOptions.EnableAdaptiveSampling;
});
```

Then enable the middleware:

```csharp
app.UseHttpLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Emit Business Telemetry

Open `ApplicationInsightsDocumentActivityMonitor.cs` and implement search telemetry:

Framework telemetry tells you that a request happened. Business telemetry tells you what the request meant for the document workflow.

```csharp
_logger.LogInformation(
	"Document search completed. CacheHit={CacheHit} ResultCount={ResultCount}",
	cacheHit,
	resultCount);

_telemetryClient.TrackEvent(
	"Documents.Search.Completed",
	new Dictionary<string, string>
	{
		["CacheHit"] = cacheHit.ToString(),
		["HasQuery"] = (!string.IsNullOrWhiteSpace(criteria.Query)).ToString(),
	},
	new Dictionary<string, double>
	{
		["ResultCount"] = resultCount,
	});
```

Use the same pattern for upload and download:

```csharp
_telemetryClient.TrackEvent(
	"Documents.Upload.Completed",
	new Dictionary<string, string>
	{
		["DocumentId"] = document.Id,
		["ContentType"] = document.ContentType ?? string.Empty,
	},
	new Dictionary<string, double>
	{
		["SizeBytes"] = document.Size ?? 0,
		["DurationMs"] = durationMs,
	});
```

<div class="tip" data-title="Telemetry is useful when it is structured">

> Prefer named properties like `DocumentId`, `ContentType`, `DurationMs`, and `CacheHit` over long free-text messages. They are easier to query later.

</div>

## Build And Try It

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

Send a request with a correlation id:

```txt
X-Correlation-Id: workshop-correlation-id
```

<div class="task" data-title="Validation">

> Confirm that the response includes the same `X-Correlation-Id` value.
>
> If Application Insights is configured, run a document workflow and inspect the emitted custom events and metrics.

</div>

---

## Closing the workshop

Once you're done with this lab you can delete the resource group you created at the beginning.

To do so, click on **Delete resource group** in the Azure Portal to delete all the resources at once. The following Az-Cli command can also be used to delete the resource group:

```bash
# Delete the resource group with all the resources
az group delete --name <resource-group>
```

