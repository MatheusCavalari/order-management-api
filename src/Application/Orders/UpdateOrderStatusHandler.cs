using Application.Dtos;
using Application.Events;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class UpdateOrderStatusHandler
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IDomainEventDispatcher _dispatcher;

    public UpdateOrderStatusHandler(IOrderRepository orders, IProductRepository products, IDomainEventDispatcher dispatcher)
    {
        _orders = orders;
        _products = products;
        _dispatcher = dispatcher;
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

        var domainEvents = order.PullDomainEvents();
        try
        {
            await _dispatcher.DispatchAsync(domainEvents);
        }
        catch
        {
            // A notification failure must never fail an otherwise-successful status update.
            // Task 4 wires a real ILogger-based implementation; this handler only guarantees
            // the exception does not propagate.
        }

        return GetOrdersHandler.ToDto(order);
    }
}
