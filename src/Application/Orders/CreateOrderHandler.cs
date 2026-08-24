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

        foreach (var line in request.Lines)
        {
            var product = await _products.GetByIdAsync(line.ProductId)
                ?? throw new InsufficientStockException(line.ProductId, line.Quantity, 0);
            if (line.Quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(line.ProductId, line.Quantity, product.StockQuantity);
            }
            resolvedProducts.Add((product, line.Quantity));
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
