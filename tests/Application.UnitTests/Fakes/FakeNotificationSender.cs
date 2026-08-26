using Application.Notifications;
using Domain;

namespace Application.UnitTests.Fakes;

public record SentNotification(string CustomerEmail, Guid OrderId, OrderStatus OldStatus, OrderStatus NewStatus);

public class FakeNotificationSender : INotificationSender
{
    public readonly List<SentNotification> Sent = new();

    public Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        Sent.Add(new SentNotification(customerEmail, orderId, oldStatus, newStatus));
        return Task.CompletedTask;
    }
}
