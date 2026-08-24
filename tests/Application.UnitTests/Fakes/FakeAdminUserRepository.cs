using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeAdminUserRepository : IAdminUserRepository
{
    private readonly Dictionary<string, AdminUser> _users = new();

    public void Seed(AdminUser user) => _users[user.Username] = user;

    public Task<AdminUser?> GetByUsernameAsync(string username) =>
        Task.FromResult(_users.GetValueOrDefault(username));
}
