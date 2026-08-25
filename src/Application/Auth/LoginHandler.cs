using Application.Repositories;

namespace Application.Auth;

public class LoginHandler
{
    private readonly IAdminUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokenGenerator;

    public LoginHandler(IAdminUserRepository users, IPasswordHasher hasher, ITokenGenerator tokenGenerator)
    {
        _users = users;
        _hasher = hasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<string?> HandleAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user is null || !_hasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return _tokenGenerator.GenerateToken(user);
    }
}
