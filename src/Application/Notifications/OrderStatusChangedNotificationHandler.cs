using Application.Events;
using Application.Repositories;
using Domain.Events;

namespace Application.Notifications;

public class OrderStatusChangedNotificationHandler : IDomainEventHandler<OrderStatusChangedEvent>
{
    private readonly ICustomerRepository _customers;
    private readonly INotificationSender _sender;

    public OrderStatusChangedNotificationHandler(ICustomerRepository customers, INotificationSender sender)
    {
        _customers = customers;
        _sender = sender;
    }

    public async Task HandleAsync(OrderStatusChangedEvent domainEvent)
    {
        var customer = await _customers.GetByIdAsync(domainEvent.CustomerId);
        if (customer is null)
        {
            return;
        }

        await _sender.SendOrderStatusChangedAsync(
            customer.Email,
            domainEvent.OrderId,
            domainEvent.OldStatus,
            domainEvent.NewStatus);
    }
}
