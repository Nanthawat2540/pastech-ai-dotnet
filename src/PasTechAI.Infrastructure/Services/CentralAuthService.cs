using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PasTechAI.Domain.Interfaces;

namespace PasTechAI.Infrastructure.Services;

public class CentralAuthService(IConfiguration config) : ICentralAuthService
{
    private readonly string _secret = config["CentralAuth:JwtSecret"]
        ?? throw new InvalidOperationException("CentralAuth:JwtSecret not configured");

    private readonly string _erpAuthUrl = config["CentralAuth:ErpAuthUrl"]
        ?? "http://erp-auth:3001";

    // Roles that are allowed to access ERP data
    private static readonly HashSet<string> ErpRoles =
        ["admin", "superadmin", "manager", "erp_user", "accountant"];

    public AuthUser? ValidateToken(string bearerToken)
    {
        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(bearerToken, new TokenValidationParameters
            {
                ValidateIssuer   = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ClockSkew        = TimeSpan.Zero,
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;

            // erp-auth JWT claims: sub, companyId, branchId, roles[], permissions[]
            var sub   = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? "";
            var roles = jwt.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToArray();

            // fallback: roles might be a JSON array in a single claim
            if (roles.Length == 0)
            {
                var rolesClaim = jwt.Claims.FirstOrDefault(c => c.Type == "roles")?.Value;
                if (rolesClaim != null)
                {
                    try { roles = JsonSerializer.Deserialize<string[]>(rolesClaim) ?? []; }
                    catch { roles = [rolesClaim]; }
                }
            }

            var email       = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "";
            var displayName = jwt.Claims.FirstOrDefault(c => c.Type is "name" or "display_name" or "username")?.Value
                           ?? jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                           ?? sub;
            var primaryRole = roles.FirstOrDefault() ?? "user";

            if (!int.TryParse(sub, out var userId)) userId = 0;

            return new AuthUser(userId, email, displayName, primaryRole, email)
            {
                Roles = roles
            };
        }
        catch { return null; }
    }

    public bool HasErpAccess(AuthUser user) =>
        user.Roles.Any(r => ErpRoles.Contains(r.ToLowerInvariant()))
        || ErpRoles.Contains(user.Role.ToLowerInvariant());
}
