namespace Domain.Exceptions;

public class InvalidOrderStatusTransitionException : DomainException
{
    public InvalidOrderStatusTransitionException(OrderStatus from, OrderStatus to)
        : base($"Cannot transition an order from {from} to {to}.") { }
}
