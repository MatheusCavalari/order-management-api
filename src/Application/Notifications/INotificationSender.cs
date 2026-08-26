using Domain;

namespace Application.Notifications;

public interface INotificationSender
{
    Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus);
}
