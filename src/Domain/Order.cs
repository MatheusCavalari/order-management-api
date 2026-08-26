using Domain.Events;
using Domain.Exceptions;

namespace Domain;

public class Order
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
    };

    private readonly List<OrderItem> _items = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;

    private Order() { }

    public static Order Create(Guid customerId, IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
        };
        order._items.AddRange(items);
        return order;
    }

    public void AdvanceTo(OrderStatus newStatus)
    {
        if (!ValidTransitions[Status].Contains(newStatus))
        {
            throw new InvalidOrderStatusTransitionException(Status, newStatus);
        }

        var oldStatus = Status;
        Status = newStatus;
        _domainEvents.Add(new OrderStatusChangedEvent(Id, CustomerId, oldStatus, newStatus));
    }

    public void Cancel()
    {
        AdvanceTo(OrderStatus.Cancelled);
    }

    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}
