using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Application.Common;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;

namespace MiniErp.Infrastructure.Identity;

public static class AppClaims
{
    public const string TenantId = "tenant_id";
    public const string TenantName = "tenant_name";
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "MiniErp";
    public string Audience { get; set; } = "MiniErp.Web";

    /// <summary>HMAC signing key (min 32 bytes). Supplied via environment / user-secrets - never committed.</summary>
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>Resolves the caller from JWT claims. Background jobs/seeding can run "as" a tenant via <see cref="RunAs"/>.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private (Guid TenantId, Guid UserId, UserRole Role)? _override;

    public Guid? UserId => _override?.UserId ?? ParseGuid(JwtRegisteredClaimNames.Sub);
    public Guid? TenantId => _override?.TenantId ?? ParseGuid(AppClaims.TenantId);

    public UserRole? Role => _override?.Role
        ?? (Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null);

    public void RunAs(Guid tenantId, Guid userId, UserRole role) => _override = (tenantId, userId, role);

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    private Guid? ParseGuid(string claim) =>
        Guid.TryParse(Principal?.FindFirstValue(claim), out var id) ? id : null;
}

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    // ASP.NET Core Identity's PBKDF2 hasher (versioned format, upgrades transparently).
    private readonly PasswordHasher<AppUser> _inner = new();

    public string Hash(AppUser user, string password) => _inner.HashPassword(user, password);

    public bool Verify(AppUser user, string password) =>
        _inner.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken CreateToken(AppUser user, Tenant tenant)
    {
        var o = options.Value;
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(o.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(AppClaims.TenantId, tenant.Id.ToString()),
            new(AppClaims.TenantName, tenant.Name)
        };

        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims,
            notBefore: clock.GetUtcNow().UtcDateTime, expires: expires,
            signingCredentials: new SigningCredentials(o.SigningKey, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

/// <summary>TimeProvider whose "local" time is the business time zone (default Asia/Kolkata),
/// so "today" is correct even when the server/container runs in UTC.</summary>
public sealed class BusinessTimeProvider(string timeZoneId) : TimeProvider
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    public override TimeZoneInfo LocalTimeZone => _zone;
}
