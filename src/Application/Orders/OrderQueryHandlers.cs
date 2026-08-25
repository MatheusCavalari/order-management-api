using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class GetOrdersHandler
{
    private readonly IOrderRepository _orders;
    public GetOrdersHandler(IOrderRepository orders) => _orders = orders;

    public async Task<IReadOnlyList<OrderDto>> HandleAsync(OrderStatus? statusFilter)
    {
        var orders = await _orders.GetAllAsync(statusFilter);
        return orders.Select(ToDto).ToList();
    }

    internal static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Status.ToString(),
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPriceAtOrderTime)).ToList());
}

public class GetOrderByIdHandler
{
    private readonly IOrderRepository _orders;
    public GetOrderByIdHandler(IOrderRepository orders) => _orders = orders;

    public async Task<OrderDto?> HandleAsync(Guid id)
    {
        var order = await _orders.GetByIdAsync(id);
        return order is null ? null : GetOrdersHandler.ToDto(order);
    }
}
