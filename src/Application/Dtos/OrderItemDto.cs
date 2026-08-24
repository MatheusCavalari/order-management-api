namespace Application.Dtos;

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPriceAtOrderTime);
