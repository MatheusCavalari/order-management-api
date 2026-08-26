namespace Domain.Exceptions;

/// <summary>
/// Thrown when order creation exhausts its optimistic-concurrency retry budget: repeated
/// RowVersion conflicts on one or more products prevented the order from being placed, even
/// though this says nothing definitive about actual stock availability (the last attempt's
/// products may or may not have had enough stock -- the point is that we could not get a clean,
/// non-conflicting write within the retry budget).
/// </summary>
public class OrderConcurrencyException : DomainException
{
    public OrderConcurrencyException()
        : base("Could not place the order because of repeated conflicting updates to product stock. Please try again.") { }
}
