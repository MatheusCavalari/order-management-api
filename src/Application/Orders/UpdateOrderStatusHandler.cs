using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class UpdateOrderStatusHandler
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;

    public UpdateOrderStatusHandler(IOrderRepository orders, IProductRepository products)
    {
        _orders = orders;
        _products = products;
    }

    public async Task<OrderDto> HandleAsync(Guid orderId, OrderStatus newStatus)
    {
        var order = await _orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order.AdvanceTo(newStatus);

        if (newStatus == OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                var product = await _products.GetByIdAsync(item.ProductId);
                product?.IncreaseStock(item.Quantity);
            }
            await _products.SaveChangesAsync();
        }

        await _orders.SaveChangesAsync();
        return GetOrdersHandler.ToDto(order);
    }
}
