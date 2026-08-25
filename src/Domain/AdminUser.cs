namespace Domain;

public class AdminUser
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }

    public AdminUser(Guid id, string username, string passwordHash)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
    }

    private AdminUser() { Username = string.Empty; PasswordHash = string.Empty; }
}
