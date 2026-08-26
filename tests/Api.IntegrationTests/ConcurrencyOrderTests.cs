using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class ConcurrencyOrderTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ConcurrencyOrderTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Two_concurrent_orders_for_same_product_both_succeed_and_stock_depletes_correctly()
    {
        // Arrange: seed one product with stock = 2
        var productId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product(productId, "Widget", 9.99m, 2));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        // Act: two concurrent POST /api/orders, each ordering 1 unit of the same product
        var task1 = client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Alice",
            customerEmail = "alice@example.com",
            lines = new[] { new { productId, quantity = 1 } }
        });

        var task2 = client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Bob",
            customerEmail = "bob@example.com",
            lines = new[] { new { productId, quantity = 1 } }
        });

        await Task.WhenAll(task1, task2);
        var response1 = await task1;
        var response2 = await task2;

        // Assert: both should succeed (stock was available)
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>();
        var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order1);
        Assert.NotNull(order2);

        // Assert: stock should now be 0
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.FindAsync(productId);
            Assert.Equal(0, product!.StockQuantity);
        }
    }

    [Fact]
    public async Task Two_concurrent_orders_when_only_one_unit_available_one_fails()
    {
        // Arrange: seed one product with stock = 1
        var productId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product(productId, "Widget", 9.99m, 1));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        // Act: two concurrent POST /api/orders, each ordering 1 unit
        var task1 = client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Alice",
            customerEmail = "alice@example.com",
            lines = new[] { new { productId, quantity = 1 } }
        });

        var task2 = client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Bob",
            customerEmail = "bob@example.com",
            lines = new[] { new { productId, quantity = 1 } }
        });

        await Task.WhenAll(task1, task2);
        var response1 = await task1;
        var response2 = await task2;

        // Assert: one should succeed (200), one should fail (422 InsufficientStock)
        var codes = new[] { response1.StatusCode, response2.StatusCode };
        Assert.Single(codes, c => c == HttpStatusCode.OK);
        Assert.Single(codes, c => c == (HttpStatusCode)422); // Unprocessable Entity (stock unavailable)

        // Assert: stock should be 0 (only one order succeeded)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.FindAsync(productId);
            Assert.Equal(0, product!.StockQuantity);
        }
    }
}
