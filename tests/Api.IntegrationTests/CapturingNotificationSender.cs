using System.Collections.Concurrent;
using Application.Notifications;
using Domain;

namespace Api.IntegrationTests;

/// <summary>
/// Test-only INotificationSender that records every call instead of writing to the console,
/// so integration tests can assert the notification pipeline actually fired end-to-end.
/// Registered as a singleton in TestApiFactory, so the captured list is shared across the
/// whole test class fixture - callers should filter by OrderId rather than assuming an
/// empty or single-item list, since other tests in the same fixture may also trigger sends.
/// </summary>
public class CapturingNotificationSender : INotificationSender
{
    public record CapturedNotification(string CustomerEmail, Guid OrderId, OrderStatus OldStatus, OrderStatus NewStatus);

    private readonly ConcurrentBag<CapturedNotification> _sent = new();

    public IReadOnlyCollection<CapturedNotification> Sent => _sent.ToList();

    public Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        _sent.Add(new CapturedNotification(customerEmail, orderId, oldStatus, newStatus));
        return Task.CompletedTask;
    }
}
