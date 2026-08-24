using Application.Products;
using Application.UnitTests.Fakes;
using Domain;
using Xunit;

namespace Application.UnitTests;

public class ProductHandlersTests
{
    [Fact]
    public async Task CreateProductHandler_adds_and_returns_product()
    {
        var repo = new FakeProductRepository();
        var handler = new CreateProductHandler(repo);

        var result = await handler.HandleAsync(new CreateProductRequest("Widget", 9.99m, 100));

        Assert.Equal("Widget", result.Name);
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task DeleteProductHandler_removes_product()
    {
        var id = Guid.NewGuid();
        var repo = new FakeProductRepository();
        repo.Seed(new Product(id, "Widget", 9.99m, 100));
        var handler = new DeleteProductHandler(repo);

        await handler.HandleAsync(id);

        Assert.Empty(await repo.GetAllAsync());
    }
}
