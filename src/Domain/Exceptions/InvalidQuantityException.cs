namespace Domain.Exceptions;

public class InvalidQuantityException : DomainException
{
    public InvalidQuantityException(int quantity)
        : base($"Quantity must be greater than zero, but {quantity} was requested.") { }
}
