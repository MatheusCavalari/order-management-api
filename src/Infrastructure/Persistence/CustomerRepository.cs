using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;
    public CustomerRepository(AppDbContext db) => _db = db;

    public Task<Customer?> GetByEmailAsync(string email) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

    public Task<Customer?> GetByIdAsync(Guid id) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(Customer customer) =>
        await _db.Customers.AddAsync(customer);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
