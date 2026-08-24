using Domain;

namespace Application.Auth;

public interface ITokenGenerator
{
    string GenerateToken(AdminUser user);
}
