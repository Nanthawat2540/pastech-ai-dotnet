namespace PasTechAI.Domain.Interfaces;

public class AuthUser(int userId, string username, string displayName, string role, string email)
{
    public int      UserId      { get; } = userId;
    public string   Username    { get; } = username;
    public string   DisplayName { get; } = displayName;
    public string   Role        { get; } = role;
    public string   Email       { get; } = email;
    public string[] Roles       { get; init; } = [role];
}

public interface ICentralAuthService
{
    AuthUser? ValidateToken(string bearerToken);
    bool HasErpAccess(AuthUser user);
}
