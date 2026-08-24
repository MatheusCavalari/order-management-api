namespace Application.Dtos;

public record ProductDto(Guid Id, string Name, decimal Price, int StockQuantity);
