using Application.Notifications;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Events;
using Xunit;

namespace Application.UnitTests;

public class OrderStatusChangedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_sends_notification_with_the_customers_email()
    {
        var customerId = Guid.NewGuid();
        var customers = new FakeCustomerRepository();
        customers.Seed(new Customer(customerId, "Ada Lovelace", "ada@example.com"));
        var sender = new FakeNotificationSender();
        var handler = new OrderStatusChangedNotificationHandler(customers, sender);
        var orderId = Guid.NewGuid();

        await handler.HandleAsync(new OrderStatusChangedEvent(orderId, customerId, OrderStatus.Pending, OrderStatus.Paid));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("ada@example.com", sent.CustomerEmail);
        Assert.Equal(orderId, sent.OrderId);
        Assert.Equal(OrderStatus.Pending, sent.OldStatus);
        Assert.Equal(OrderStatus.Paid, sent.NewStatus);
    }

    [Fact]
    public async Task HandleAsync_does_nothing_when_customer_not_found()
    {
        var customers = new FakeCustomerRepository();
        var sender = new FakeNotificationSender();
        var handler = new OrderStatusChangedNotificationHandler(customers, sender);

        await handler.HandleAsync(new OrderStatusChangedEvent(Guid.NewGuid(), Guid.NewGuid(), OrderStatus.Pending, OrderStatus.Paid));

        Assert.Empty(sender.Sent);
    }
}
