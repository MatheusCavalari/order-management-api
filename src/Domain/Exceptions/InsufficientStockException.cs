namespace Domain.Exceptions;

public class InsufficientStockException : DomainException
{
    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Product {productId} has {available} in stock, but {requested} were requested.") { }
}
