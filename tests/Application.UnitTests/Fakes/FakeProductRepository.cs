using System.Reflection;
using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Fakes;

public class FakeProductRepository : IProductRepository
{
    private static readonly MethodInfo StockQuantitySetter =
        typeof(Product).GetProperty(nameof(Product.StockQuantity))!.GetSetMethod(nonPublic: true)!;

    // Same-instance "identity map" per product id, mirroring EF's change tracker: GetByIdAsync
    // always hands back the same tracked Product instance, so in-place mutations (DecreaseStock)
    // are visible across calls -- including across a failed-and-retried attempt.
    private readonly Dictionary<Guid, Product> _tracked = new();

    // The last successfully-committed StockQuantity per product, i.e. what's actually "in the
    // database". A failed SaveChangesAsync (simulated concurrency conflict) never updates this,
    // mirroring EF/SQL rolling back the whole transaction on a concurrency conflict.
    private readonly Dictionary<Guid, int> _persistedStock = new();

    // Number of remaining SaveChangesAsync calls that should throw a simulated
    // DbUpdateConcurrencyException before calls start succeeding.
    private int _remainingConcurrencyFailures;

    public void Seed(Product product)
    {
        _tracked[product.Id] = product;
        _persistedStock[product.Id] = product.StockQuantity;
    }

    /// <summary>
    /// Configures the next <paramref name="times"/> call(s) to <see cref="SaveChangesAsync"/> to throw
    /// a simulated <see cref="DbUpdateConcurrencyException"/> (empty Entries, matching how the fix under
    /// test relies on <see cref="ReloadModifiedAsync"/> rather than ex.Entries). Calls after that succeed.
    /// </summary>
    public void FailNextSaveChangesWithConcurrencyException(int times = 1) =>
        _remainingConcurrencyFailures += times;

    public Task<IReadOnlyList<Product>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Product>>(_tracked.Values.ToList());

    public Task<Product?> GetByIdAsync(Guid id) =>
        Task.FromResult(_tracked.GetValueOrDefault(id));

    public Task AddAsync(Product product)
    {
        _tracked[product.Id] = product;
        if (!_persistedStock.ContainsKey(product.Id))
        {
            _persistedStock[product.Id] = product.StockQuantity;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _tracked.Remove(id);
        _persistedStock.Remove(id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        if (_remainingConcurrencyFailures > 0)
        {
            _remainingConcurrencyFailures--;
            // Real EF rolls back the entire transaction on a concurrency conflict, so nothing is
            // actually persisted. We deliberately leave _persistedStock untouched here, and -- just
            // like real EF -- we do NOT revert the in-memory tracked Product instances either. That's
            // the condition ReloadModifiedAsync exists to clean up.
            throw new DbUpdateConcurrencyException("Simulated optimistic concurrency conflict.");
        }

        foreach (var (id, product) in _tracked)
        {
            _persistedStock[id] = product.StockQuantity;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates EF's <c>EntityEntry.ReloadAsync()</c> for every tracked product whose in-memory
    /// StockQuantity has drifted from the last persisted value: resets it back to that persisted value,
    /// discarding the in-memory change. Uses reflection to bypass Product's domain-validated setters,
    /// the same way EF Core's change tracker writes CurrentValues without going through domain methods.
    /// </summary>
    public Task ReloadModifiedAsync()
    {
        foreach (var (id, product) in _tracked)
        {
            if (_persistedStock.TryGetValue(id, out var persistedStock) && product.StockQuantity != persistedStock)
            {
                StockQuantitySetter.Invoke(product, new object[] { persistedStock });
            }
        }
        return Task.CompletedTask;
    }
}
