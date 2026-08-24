using Domain;

namespace Application.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter);
    Task<Order?> GetByIdAsync(Guid id);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
