using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = new();

    public void Seed(Customer customer) => _customers[customer.Id] = customer;

    public IReadOnlyList<Customer> Customers => _customers.Values.ToList();

    public Task<Customer?> GetByEmailAsync(string email) =>
        Task.FromResult(_customers.Values.FirstOrDefault(c => c.Email == email));

    public Task<Customer?> GetByIdAsync(Guid id) =>
        Task.FromResult(_customers.GetValueOrDefault(id));

    public Task AddAsync(Customer customer)
    {
        _customers[customer.Id] = customer;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
