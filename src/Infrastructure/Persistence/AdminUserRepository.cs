using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly AppDbContext _db;
    public AdminUserRepository(AppDbContext db) => _db = db;

    public Task<AdminUser?> GetByUsernameAsync(string username) =>
        _db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
}
