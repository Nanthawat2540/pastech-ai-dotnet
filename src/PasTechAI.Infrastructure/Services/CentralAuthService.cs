using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Services;

public class CentralAuthService(IConfiguration config) : ICentralAuthService
{
    private readonly string _secret = config["CentralAuth:JwtSecret"]
        ?? throw new InvalidOperationException("CentralAuth:JwtSecret not configured");

    // Roles that are allowed to access ERP data
    private static readonly HashSet<string> ErpRoles =
        ["admin", "erp", "manager"];

    public AuthUser? ValidateToken(string bearerToken)
    {
        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(bearerToken, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = "central-auth",
                ValidateAudience         = false,
                ValidateLifetime         = true,
                IssuerSigningKey         = key,
                ClockSkew                = TimeSpan.Zero,
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;
            return new AuthUser(
                UserId:      int.Parse(jwt.Claims.First(c => c.Type == "uid").Value),
                Username:    jwt.Claims.First(c => c.Type == "username").Value,
                DisplayName: jwt.Claims.First(c => c.Type == "display_name").Value,
                Role:        jwt.Claims.First(c => c.Type == "role").Value,
                Email:       jwt.Claims.First(c => c.Type == "email").Value);
        }
        catch { return null; }
    }

    public bool HasErpAccess(AuthUser user) =>
        ErpRoles.Contains(user.Role.ToLowerInvariant());
}
