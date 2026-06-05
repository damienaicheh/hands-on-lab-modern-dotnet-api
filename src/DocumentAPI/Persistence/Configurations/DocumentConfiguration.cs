namespace DocumentAPI.Persistence.Configurations;

using DocumentAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="Document" /> entity.
/// </summary>
internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents", "dbo");

        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).HasMaxLength(64);
        builder.Property(document => document.FileName).IsRequired().HasMaxLength(260);
        builder.Property(document => document.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(document => document.Title).HasMaxLength(512);
        builder.Property(document => document.Source).HasMaxLength(256);
        builder.Property(document => document.ContentHash).IsRequired().IsFixedLength().HasMaxLength(64);
        builder.Property(document => document.StorageKey).IsRequired().HasMaxLength(512);

        builder.Property(document => document.Tags).HasColumnName("TagsJson");

        builder.HasIndex(document => document.ContentHash)
            .IsUnique()
            .HasDatabaseName("IX_Documents_ContentHash");
    }
}
