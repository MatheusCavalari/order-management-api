namespace Api.Contracts;

public record CreateOrderLineApiRequest(Guid ProductId, int Quantity);
public record CreateOrderApiRequest(Guid CustomerId, IReadOnlyList<CreateOrderLineApiRequest> Lines);
