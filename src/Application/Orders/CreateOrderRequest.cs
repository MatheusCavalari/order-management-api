namespace Application.Orders;

public record CreateOrderLineRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(Guid CustomerId, IReadOnlyList<CreateOrderLineRequest> Lines);
