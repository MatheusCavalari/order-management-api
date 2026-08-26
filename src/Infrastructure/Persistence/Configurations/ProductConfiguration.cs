using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        // SQLite has no native rowversion/timestamp type, so there is no database trigger or
        // computed column to auto-generate a new value on write. IsRowVersion() marks the column
        // as store-generated (ValueGeneratedOnAddOrUpdate), which tells EF to exclude it from the
        // INSERT/UPDATE column list entirely and rely on the database to produce a new value -
        // something SQLite never does here, leaving RowVersion permanently NULL and defeating the
        // optimistic concurrency check (every UPDATE's WHERE clause compared against NULL, so it
        // always matched and concurrent writes never conflicted). Instead, treat it purely as a
        // client-generated concurrency token: EF compares old vs. new values in the WHERE clause,
        // and AppDbContext.SaveChanges/SaveChangesAsync assigns a fresh value on every insert/update.
        builder.Property(p => p.RowVersion)
            .IsConcurrencyToken();
    }
}
