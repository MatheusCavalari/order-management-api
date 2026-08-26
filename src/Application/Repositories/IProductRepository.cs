using Domain;

namespace Application.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(Guid id);
    Task AddAsync(Product product);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();

    /// <summary>
    /// Discards any pending in-memory changes on every currently-tracked, modified <see cref="Product"/>
    /// by reloading it from the persisted store. Used after a concurrency conflict to make sure a retry
    /// starts from a clean, up-to-date snapshot for ALL products touched by the current unit of work --
    /// not just the ones that were reported in the failing exception -- since a failed save can leave
    /// unrelated modified entities holding stale, already-applied in-memory changes (e.g. a stock
    /// decrement) that would otherwise be applied a second time on retry.
    /// </summary>
    Task ReloadModifiedAsync();
}
