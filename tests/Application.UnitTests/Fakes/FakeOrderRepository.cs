using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeOrderRepository : IOrderRepository
{
    public readonly List<Order> Orders = new();

    public Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter) =>
        Task.FromResult<IReadOnlyList<Order>>(
            Orders.Where(o => statusFilter == null || o.Status == statusFilter).ToList());

    public Task<Order?> GetByIdAsync(Guid id) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(Order order)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
