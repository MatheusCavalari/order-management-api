using System.ComponentModel.DataAnnotations;
using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Products;

public record CreateProductRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(0.01, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int StockQuantity);

public record UpdateProductRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Range(0.01, double.MaxValue)] decimal Price);

public class GetProductsHandler
{
    private readonly IProductRepository _products;
    public GetProductsHandler(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<ProductDto>> HandleAsync()
    {
        var products = await _products.GetAllAsync();
        return products.Select(ToDto).ToList();
    }

    internal static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Price, p.StockQuantity);
}

public class CreateProductHandler
{
    private readonly IProductRepository _products;
    public CreateProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductDto> HandleAsync(CreateProductRequest request)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Price, request.StockQuantity);
        await _products.AddAsync(product);
        await _products.SaveChangesAsync();
        return GetProductsHandler.ToDto(product);
    }
}

public class UpdateProductHandler
{
    private readonly IProductRepository _products;
    public UpdateProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductDto?> HandleAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null) return null;

        product.UpdateDetails(request.Name, request.Price);
        await _products.SaveChangesAsync();
        return GetProductsHandler.ToDto(product);
    }
}

public class DeleteProductHandler
{
    private readonly IProductRepository _products;
    public DeleteProductHandler(IProductRepository products) => _products = products;

    public async Task HandleAsync(Guid id)
    {
        await _products.DeleteAsync(id);
        await _products.SaveChangesAsync();
    }
}
