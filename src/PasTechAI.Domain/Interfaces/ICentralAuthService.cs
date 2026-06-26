namespace PasTechAI.Domain.Interfaces;

public record AuthUser(
    int    UserId,
    string Username,
    string DisplayName,
    string Role,
    string Email);

public interface ICentralAuthService
{
    AuthUser? ValidateToken(string bearerToken);
    bool HasErpAccess(AuthUser user);
}
