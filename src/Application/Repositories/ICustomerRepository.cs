using Domain;

namespace Application.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer?> GetByIdAsync(Guid id);
    Task AddAsync(Customer customer);
    Task SaveChangesAsync();
}
