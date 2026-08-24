using Application.Auth;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api;

public static class DevSeed
{
    public static void EnsureSeeded(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        if (!db.AdminUsers.Any())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.AdminUsers.Add(new AdminUser(Guid.NewGuid(), "admin", hasher.Hash("changeme")));
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new Product(Guid.NewGuid(), "Widget", 9.99m, 50),
                new Product(Guid.NewGuid(), "Gadget", 19.99m, 30),
                new Product(Guid.NewGuid(), "Gizmo", 29.99m, 15));
        }

        db.SaveChanges();
    }
}
