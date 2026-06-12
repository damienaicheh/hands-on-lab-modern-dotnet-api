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

<div class="warning" data-title="Production warning">

> For this hands-on lab, applying migrations at startup keeps the environment simple and lets you see the database schema appear immediately.
>
> In production, this pattern can create concurrency risks, slow application startup, and cause incidents during multi-instance deployments. Prefer applying migrations from the deployment pipeline instead, for example with a dedicated job, a migration bundle, or an approved database deployment step that runs before the API starts serving traffic.

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

## Start The Project

Start the project using the **Run** button in your Visual Studio or the following command lines:

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