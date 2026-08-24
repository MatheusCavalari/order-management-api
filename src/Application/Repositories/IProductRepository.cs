using Domain;

namespace Application.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(Guid id);
    Task AddAsync(Product product);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
