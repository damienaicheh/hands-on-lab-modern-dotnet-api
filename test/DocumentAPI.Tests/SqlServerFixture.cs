namespace DocumentAPI.Tests;

using Testcontainers.MsSql;

/// <summary>
/// Provides a shared SQL Server container for the integration tests.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>
    /// Gets the connection string pointing to the running SQL Server container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public Task InitializeAsync() => _container.StartAsync();

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync();
}

/// <summary>
/// Groups the integration tests that share a single SQL Server container instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    /// <summary>
    /// The xUnit collection name shared by the SQL Server-backed integration tests.
    /// </summary>
    public const string Name = "SQL Server integration tests";
}
