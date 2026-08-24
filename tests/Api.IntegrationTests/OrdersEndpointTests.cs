using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Auth;
using Application.Dtos;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class OrdersEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public OrdersEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_order_for_nonexistent_product_returns_422_ProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = Guid.NewGuid(),
            lines = new[] { new { productId = Guid.NewGuid(), quantity = 1 } },
        });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    // Risk-area coverage: Order.Items is mapped via OwnsMany + PropertyAccessMode.Field against the
    // private backing field Order._items. That mapping had only been reasoned through statically before
    // this task. This test is the first point where a successful order creation actually round-trips
    // through SaveChanges() and a subsequent query against real SQLite, exercising that mapping for real.
    [Fact]
    public async Task Create_order_for_existing_product_persists_and_roundtrips_order_items()
    {
        var productId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product(productId, "Widget", 9.99m, 10));
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var customerId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            lines = new[] { new { productId, quantity = 3 } },
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(created);
        var order = created!;
        var orderItem = Assert.Single(order.Items);
        Assert.Equal(productId, orderItem.ProductId);
        Assert.Equal(3, orderItem.Quantity);
        Assert.Equal(9.99m, orderItem.UnitPriceAtOrderTime);

        // Fetch the order back through GET /api/orders/{id} (requires auth) to prove the items were
        // actually persisted to and reloaded from the database, not merely echoed back from memory.
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var getResponse = await client.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(fetched);
        var fetchedItem = Assert.Single(fetched!.Items);
        Assert.Equal(productId, fetchedItem.ProductId);
        Assert.Equal(3, fetchedItem.Quantity);
        Assert.Equal(9.99m, fetchedItem.UnitPriceAtOrderTime);
    }

    private async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        const string username = "orders-test-admin";
        const string password = "correct-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.AdminUsers.Add(new AdminUser(Guid.NewGuid(), username, hasher.Hash(password)));
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["token"];
    }
}
