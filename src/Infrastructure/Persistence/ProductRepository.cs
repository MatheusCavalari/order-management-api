using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.ToListAsync();

    public Task<Product?> GetByIdAsync(Guid id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddAsync(Product product) =>
        await _db.Products.AddAsync(product);

    public async Task DeleteAsync(Guid id)
    {
        var product = await GetByIdAsync(id);
        if (product is not null)
        {
            _db.Products.Remove(product);
        }
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
