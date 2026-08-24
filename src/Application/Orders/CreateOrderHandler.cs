using Application.Dtos;
using Application.Repositories;
using Domain;
using Domain.Exceptions;

namespace Application.Orders;

public class CreateOrderHandler
{
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;

    public CreateOrderHandler(IProductRepository products, IOrderRepository orders)
    {
        _products = products;
        _orders = orders;
    }

    public async Task<OrderDto> HandleAsync(CreateOrderRequest request)
    {
        var resolvedProducts = new List<(Product Product, int Quantity)>();

        // Group request lines by ProductId and aggregate quantities
        var linesByProduct = request.Lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(line => line.Quantity));

        // Validate aggregated quantities BEFORE any stock decrements
        foreach (var (productId, aggregatedQuantity) in linesByProduct)
        {
            var product = await _products.GetByIdAsync(productId)
                ?? throw new InsufficientStockException(productId, aggregatedQuantity, 0);
            if (aggregatedQuantity > product.StockQuantity)
            {
                throw new InsufficientStockException(productId, aggregatedQuantity, product.StockQuantity);
            }
            resolvedProducts.Add((product, aggregatedQuantity));
        }

        var orderItems = resolvedProducts
            .Select(rp => new OrderItem(rp.Product.Id, rp.Quantity, rp.Product.Price))
            .ToList();
        var order = Order.Create(request.CustomerId, orderItems);

        foreach (var (product, quantity) in resolvedProducts)
        {
            product.DecreaseStock(quantity);
        }

        await _orders.AddAsync(order);
        await _orders.SaveChangesAsync();
        await _products.SaveChangesAsync();

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPriceAtOrderTime)).ToList());
    }
}
