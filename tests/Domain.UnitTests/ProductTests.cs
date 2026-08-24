using Domain;
using Domain.Exceptions;
using Xunit;

public class ProductTests
{
    [Fact]
    public void DecreaseStock_reduces_quantity_when_enough_stock()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 5);

        product.DecreaseStock(3);

        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_throws_when_insufficient_stock()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 2);

        Assert.Throws<InsufficientStockException>(() => product.DecreaseStock(3));
    }

    [Fact]
    public void DecreaseStock_throws_when_quantity_is_zero()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 5);

        Assert.Throws<InvalidQuantityException>(() => product.DecreaseStock(0));

        Assert.Equal(5, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_throws_when_quantity_is_negative()
    {
        // Regression test: a negative quantity must not be allowed to flow through
        // StockQuantity -= quantity, which would otherwise inflate stock.
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 5);

        Assert.Throws<InvalidQuantityException>(() => product.DecreaseStock(-50));

        Assert.Equal(5, product.StockQuantity);
    }

    [Fact]
    public void IncreaseStock_adds_quantity_back()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 2);

        product.IncreaseStock(5);

        Assert.Equal(7, product.StockQuantity);
    }
}
