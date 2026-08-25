using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter)
    {
        var query = _db.Orders.AsQueryable();
        if (statusFilter is not null)
        {
            query = query.Where(o => o.Status == statusFilter);
        }
        return await query.ToListAsync();
    }

    public Task<Order?> GetByIdAsync(Guid id) =>
        _db.Orders.FirstOrDefaultAsync(o => o.Id == id);

    public async Task AddAsync(Order order) =>
        await _db.Orders.AddAsync(order);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
