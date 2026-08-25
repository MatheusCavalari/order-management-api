using System.Net;
using System.Net.Http.Json;
using Application.Auth;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class AuthTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public AuthTests(TestApiFactory factory) => _factory = factory;

    private async Task SeedAdminAsync(string username, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.AdminUsers.Add(new AdminUser(Guid.NewGuid(), username, hasher.Hash(password)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        await SeedAdminAsync("admin1", "correct-password");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin1", password = "correct-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.False(string.IsNullOrEmpty(body!["token"]));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await SeedAdminAsync("admin2", "correct-password");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin2", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
