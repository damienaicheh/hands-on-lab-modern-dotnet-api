namespace DocumentAPI.Persistence;

using DocumentAPI.Entities;
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
    // <lab id="2">
    //|public DbSet<Document> Documents => throw new NotImplementedException("TODO Lab 2: Expose the document metadata set.");
    public DbSet<Document> Documents => Set<Document>();
    // </lab>

    /// <inheritdoc />
    // <lab id="2">
    //|// TODO Lab 2: Apply the EF Core model configuration from this assembly. By overriding the OnModelCreating method and applying configurations from the assembly, we ensure that all our entity configurations are registered without having to do it manually for each one..
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
    }
    // </lab>
}
