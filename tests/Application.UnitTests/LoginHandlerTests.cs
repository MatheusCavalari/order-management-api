using Application.Auth;
using Application.UnitTests.Fakes;
using Domain;
using Xunit;

namespace Application.UnitTests;

public class LoginHandlerTests
{
    private class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private class FakeTokenGenerator : ITokenGenerator
    {
        public string GenerateToken(AdminUser user) => $"token-for-{user.Username}";
    }

    [Fact]
    public async Task HandleAsync_returns_token_for_valid_credentials()
    {
        var users = new FakeAdminUserRepository();
        users.Seed(new AdminUser(Guid.NewGuid(), "admin", "hashed:secret"));
        var handler = new LoginHandler(users, new FakeHasher(), new FakeTokenGenerator());

        var token = await handler.HandleAsync("admin", "secret");

        Assert.Equal("token-for-admin", token);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_wrong_password()
    {
        var users = new FakeAdminUserRepository();
        users.Seed(new AdminUser(Guid.NewGuid(), "admin", "hashed:secret"));
        var handler = new LoginHandler(users, new FakeHasher(), new FakeTokenGenerator());

        var token = await handler.HandleAsync("admin", "wrong");

        Assert.Null(token);
    }
}
