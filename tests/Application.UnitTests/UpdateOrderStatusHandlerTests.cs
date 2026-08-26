using Application.Orders;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Events;
using Xunit;

namespace Application.UnitTests;

public class UpdateOrderStatusHandlerTests
{
    [Fact]
    public async Task HandleAsync_cancelling_a_pending_order_returns_stock()
    {
        var productId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(productId, "Widget", 10.00m, stockQuantity: 5));
        var order = Order.Create(Guid.NewGuid(), new[] { new OrderItem(productId, 3, 10.00m) });
        var orders = new FakeOrderRepository();
        orders.Orders.Add(order);
        var dispatcher = new FakeDomainEventDispatcher();
        var handler = new UpdateOrderStatusHandler(orders, products, dispatcher);

        await handler.HandleAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(8, (await products.GetByIdAsync(productId))!.StockQuantity);
    }

    [Fact]
    public async Task HandleAsync_dispatches_the_order_status_changed_event_after_saving()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { new OrderItem(Guid.NewGuid(), 1, 10.00m) });
        var orders = new FakeOrderRepository();
        orders.Orders.Add(order);
        var products = new FakeProductRepository();
        var dispatcher = new FakeDomainEventDispatcher();
        var handler = new UpdateOrderStatusHandler(orders, products, dispatcher);

        await handler.HandleAsync(order.Id, OrderStatus.Paid);

        var dispatched = Assert.Single(dispatcher.DispatchedEvents);
        var statusChanged = Assert.IsType<OrderStatusChangedEvent>(dispatched);
        Assert.Equal(OrderStatus.Pending, statusChanged.OldStatus);
        Assert.Equal(OrderStatus.Paid, statusChanged.NewStatus);
    }
}
