namespace Domain;

public class OrderItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPriceAtOrderTime { get; private set; }

    public OrderItem(Guid productId, int quantity, decimal unitPriceAtOrderTime)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPriceAtOrderTime = unitPriceAtOrderTime;
    }

    private OrderItem() { }
}
