namespace DocumentAPI.Persistence;

using DocumentAPI.Models;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Entity Framework Core database context for document metadata.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the document metadata set.
    /// </summary>
    public DbSet<Document> Documents => Set<Document>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
    }
}
