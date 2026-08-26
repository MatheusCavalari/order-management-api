using Application.Dtos;
using Application.Repositories;
using Domain;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Orders;

public class CreateOrderHandler
{
    private const int MaxRetries = 3;

    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly ICustomerRepository _customers;

    public CreateOrderHandler(IProductRepository products, IOrderRepository orders, ICustomerRepository customers)
    {
        _products = products;
        _orders = orders;
        _customers = customers;
    }

    public async Task<OrderDto> HandleAsync(CreateOrderRequest request)
    {
        var customer = await _customers.GetByEmailAsync(request.CustomerEmail);
        if (customer is null)
        {
            customer = new Customer(Guid.NewGuid(), request.CustomerName, request.CustomerEmail);
            await _customers.AddAsync(customer);
            await _customers.SaveChangesAsync();
        }

        // Group request lines by ProductId and aggregate quantities
        var linesByProduct = request.Lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(line => line.Quantity));

        // The order is created once and reused across retries so a concurrency conflict on
        // SaveChangesAsync doesn't leave multiple "Added" Order entities tracked by the same
        // DbContext (which would otherwise insert duplicate orders once a retry succeeds).
        Order? order = null;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var resolvedProducts = new List<(Product Product, int Quantity)>();

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

                foreach (var (product, quantity) in resolvedProducts)
                {
                    product.DecreaseStock(quantity);
                }

                if (order is null)
                {
                    var orderItems = resolvedProducts
                        .Select(rp => new OrderItem(rp.Product.Id, rp.Quantity, rp.Product.Price))
                        .ToList();
                    order = Order.Create(customer.Id, orderItems);
                    await _orders.AddAsync(order);
                }

                await _orders.SaveChangesAsync();
                await _products.SaveChangesAsync();

                return new OrderDto(
                    order.Id,
                    order.CustomerId,
                    order.Status.ToString(),
                    order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPriceAtOrderTime)).ToList());
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == MaxRetries - 1)
                {
                    // Exhausted all retries: repeated concurrency conflicts on the same
                    // product(s) almost always mean stock genuinely ran out to a faster
                    // concurrent request.
                    throw new InsufficientStockException(Guid.Empty, 0, 0);
                }

                // Another request modified a product's stock (RowVersion mismatch) between our
                // read and write. Reload the conflicting entries so the change tracker picks up
                // the current RowVersion and StockQuantity from the database. This also discards
                // our failed in-memory DecreaseStock call, so the next attempt starts from a
                // clean, up-to-date Product instance instead of double-decrementing stock on the
                // entity that EF's identity map would otherwise keep serving from cache.
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync();
                }
            }
        }

        // Unreachable: the loop above always either returns on success or throws on the
        // final attempt. Kept only to satisfy the compiler's control-flow analysis.
        throw new InsufficientStockException(Guid.Empty, 0, 0);
    }
}
