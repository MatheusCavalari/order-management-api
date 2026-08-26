namespace Domain.Events;

public record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    OrderStatus OldStatus,
    OrderStatus NewStatus) : IDomainEvent;
