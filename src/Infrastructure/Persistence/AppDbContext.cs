using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    // Product.RowVersion is a client-generated concurrency token (see ProductConfiguration) because
    // SQLite has no database-side mechanism to auto-generate a new value on write. Assign a fresh
    // value here, before every save, for any Product being inserted or updated so the optimistic
    // concurrency check has something meaningful to compare against.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampProductRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampProductRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampProductRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(p => p.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
