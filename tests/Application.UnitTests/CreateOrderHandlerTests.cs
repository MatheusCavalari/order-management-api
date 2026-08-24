using Application.Orders;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Exceptions;
using Xunit;

namespace Application.UnitTests;

public class CreateOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_order_and_decrements_stock()
    {
        var productId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(productId, "Widget", 10.00m, stockQuantity: 5));
        var orders = new FakeOrderRepository();
        var handler = new CreateOrderHandler(products, orders);

        var result = await handler.HandleAsync(new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderLineRequest(productId, 3) }));

        Assert.Equal("Pending", result.Status);
        Assert.Equal(2, (await products.GetByIdAsync(productId))!.StockQuantity);
        Assert.Single(orders.Orders);
    }

    [Fact]
    public async Task HandleAsync_rejects_whole_order_when_any_line_lacks_stock()
    {
        var plentyId = Guid.NewGuid();
        var scarceId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(plentyId, "Plenty", 5.00m, stockQuantity: 10));
        products.Seed(new Product(scarceId, "Scarce", 5.00m, stockQuantity: 1));
        var orders = new FakeOrderRepository();
        var handler = new CreateOrderHandler(products, orders);

        await Assert.ThrowsAsync<InsufficientStockException>(() => handler.HandleAsync(
            new CreateOrderRequest(Guid.NewGuid(), new[]
            {
                new CreateOrderLineRequest(plentyId, 2),
                new CreateOrderLineRequest(scarceId, 5),
            })));

        Assert.Equal(10, (await products.GetByIdAsync(plentyId))!.StockQuantity);
        Assert.Equal(1, (await products.GetByIdAsync(scarceId))!.StockQuantity);
        Assert.Empty(orders.Orders);
    }
}
