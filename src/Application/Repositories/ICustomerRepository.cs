using Domain;

namespace Application.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByEmailAsync(string email);
    Task AddAsync(Customer customer);
    Task SaveChangesAsync();
}
