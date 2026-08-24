using Domain;
using Domain.Exceptions;
using Xunit;

public class OrderTests
{
    private static OrderItem Item(int quantity = 1) =>
        new(Guid.NewGuid(), quantity, unitPriceAtOrderTime: 10.00m);

    [Fact]
    public void Create_starts_in_Pending_status()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    public void AdvanceTo_allows_valid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });
        if (from != OrderStatus.Pending)
        {
            order.AdvanceTo(from);
        }

        order.AdvanceTo(to);

        Assert.Equal(to, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void AdvanceTo_rejects_invalid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });

        // Navigate to the desired from status using valid transitions
        if (from == OrderStatus.Paid)
        {
            order.AdvanceTo(OrderStatus.Paid);
        }
        else if (from == OrderStatus.Shipped)
        {
            order.AdvanceTo(OrderStatus.Paid);
            order.AdvanceTo(OrderStatus.Shipped);
        }
        else if (from == OrderStatus.Cancelled)
        {
            order.AdvanceTo(OrderStatus.Cancelled);
        }

        Assert.Throws<InvalidOrderStatusTransitionException>(() => order.AdvanceTo(to));
    }

    [Fact]
    public void Cancel_from_Pending_marks_order_Cancelled()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_from_Shipped_throws()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });
        order.AdvanceTo(OrderStatus.Paid);
        order.AdvanceTo(OrderStatus.Shipped);

        Assert.Throws<InvalidOrderStatusTransitionException>(() => order.Cancel());
    }
}
