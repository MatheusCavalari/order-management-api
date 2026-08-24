namespace Application.Orders;

public record CreateOrderLineRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(string CustomerName, string CustomerEmail, IReadOnlyList<CreateOrderLineRequest> Lines);
