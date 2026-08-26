using Application.Notifications;
using Domain;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

public class ConsoleNotificationSender : INotificationSender
{
    private readonly ILogger<ConsoleNotificationSender> _logger;

    public ConsoleNotificationSender(ILogger<ConsoleNotificationSender> logger) => _logger = logger;

    public Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        _logger.LogInformation(
            "[Notification] {CustomerEmail}: order {OrderId} changed from {OldStatus} to {NewStatus}",
            customerEmail, orderId, oldStatus, newStatus);
        return Task.CompletedTask;
    }
}
