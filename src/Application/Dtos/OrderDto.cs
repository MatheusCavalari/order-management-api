namespace Application.Dtos;

public record OrderDto(Guid Id, Guid CustomerId, string Status, IReadOnlyList<OrderItemDto> Items);
