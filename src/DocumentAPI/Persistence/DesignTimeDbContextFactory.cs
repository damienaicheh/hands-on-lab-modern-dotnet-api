namespace DocumentAPI.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Provides a SQL Server-targeted context for design-time tooling such as <c>dotnet ef migrations</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    /// <inheritdoc />
    public DocumentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DocumentApi;Trusted_Connection=True;");

        return new DocumentDbContext(optionsBuilder.Options);
    }
}
