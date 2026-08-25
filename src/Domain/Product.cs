using Domain.Exceptions;

namespace Domain;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }

    public Product(Guid id, string name, decimal price, int stockQuantity)
    {
        Id = id;
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
    }

    private Product() { Name = string.Empty; }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidQuantityException(quantity);
        }

        if (quantity > StockQuantity)
        {
            throw new InsufficientStockException(Id, quantity, StockQuantity);
        }

        StockQuantity -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        StockQuantity += quantity;
    }

    public void UpdateDetails(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
}
