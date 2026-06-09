# Lab 2 - SQL Database and Dependency Injection

In this lab, you will add SQL Server persistence for document metadata. The API will still not upload documents end-to-end, but the persistence layer will be ready for the next labs.

The starter already provides the `Document` entity, database options, EF Core mapping, migration files, and Azure SQL authentication helper. Your job is to connect those pieces through `DocumentDbContext` and dependency injection.

At the end of this lab, the API will know how to talk to the database, even if no endpoint is using it fully yet. That lets the next labs focus on workflows instead of infrastructure setup.

## What You Will Learn

In this lab, you will:

- Expose a `DbSet<Document>` from the EF Core context.
- Apply entity configurations from the current assembly.
- Register `DocumentDbContext` in the application container.
- Apply pending migrations when the application starts.
- Build a SQL Server connection string from strongly typed options.

## Files To Open

You only need to edit these files:

- `src/DocumentAPI/Persistence/DocumentDbContext.cs`
- `src/DocumentAPI/Services/DependencyInjection.cs`

The entity, options, mappings, and migration are already provided.

## Complete The DbContext

Open `DocumentDbContext.cs` and expose the document metadata set:

The `DbContext` is the unit of work for EF Core. It is the object your services will use to query and save document metadata without writing SQL by hand.

```csharp
public DbSet<Document> Documents => Set<Document>();
```

Then apply the EF Core configurations:

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

> For this hands-on lab, applying migrations at startup keeps the environment simple. In production, database changes are usually deployed by a release pipeline.

</div>

## Configure The SQL Provider

Add the provider configuration:

The workshop uses identity-based access to Azure SQL. That means the application receives a token through `DefaultAzureCredential` instead of storing a SQL username and password in configuration.

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

```bash
dotnet build src/DocumentAPI/DocumentAPI.csproj
```

<div class="task" data-title="Validation">

> The project should build successfully.
>
> You now have SQL metadata persistence registered, even though upload is not complete yet.

</div>

---