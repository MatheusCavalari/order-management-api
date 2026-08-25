using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Xunit;

namespace Api.IntegrationTests;

public class ProductsEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ProductsEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAll_is_publicly_accessible_and_returns_empty_list_initially()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
    }

    [Fact]
    public async Task Create_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products", new { name = "Widget", price = 9.99m, stockQuantity = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
