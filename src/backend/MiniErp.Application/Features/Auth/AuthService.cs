using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;

namespace MiniErp.Application.Features.Auth;

public sealed record RegisterTenantRequest(
    string CompanyName, string StateCode, string? Gstin, string? Address,
    string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserDto(Guid UserId, string Email, string FullName, string Role, Guid TenantId, string TenantName, string TenantStateCode);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);

internal sealed class RegisterTenantValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StateCode).ValidStateCode();
        RuleFor(x => x.Gstin)
            .Must((req, gstin) => IndianStates.IsValidGstin(gstin, req.StateCode))
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin))
            .WithMessage("GSTIN is not valid for the selected state.");
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).StrongPassword();
    }
}

internal sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed partial class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    ICurrentUser currentUser,
    IValidator<RegisterTenantRequest> registerValidator,
    IValidator<LoginRequest> loginValidator)
{
    /// <summary>Self-service sign-up: creates the tenant (company) and its owner account.</summary>
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var tenant = new Tenant(request.CompanyName, await UniqueSlugAsync(request.CompanyName, ct),
            request.StateCode, request.Gstin?.Trim().ToUpperInvariant(), request.Address);
        var owner = new AppUser(tenant.Id, email, request.FullName, UserRole.Owner);
        owner.SetPasswordHash(hasher.Hash(owner, request.Password));

        db.Tenants.Add(tenant);
        db.Users.Add(owner);
        await db.SaveChangesAsync(ct);

        return Issue(owner, tenant);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        // Login happens before a tenant is known, so this is the one deliberate cross-tenant lookup.
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !hasher.Verify(user, request.Password))
            throw new AuthenticationFailedException();

        var tenant = await db.Tenants.SingleAsync(t => t.Id == user.TenantId, ct);
        return Issue(user, tenant);
    }

    public async Task<CurrentUserDto> GetCurrentAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw new AuthenticationFailedException();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == user.TenantId, ct);
        return ToDto(user, tenant);
    }

    private AuthResponse Issue(AppUser user, Tenant tenant)
    {
        var token = tokens.CreateToken(user, tenant);
        return new AuthResponse(token.Token, token.ExpiresAtUtc, ToDto(user, tenant));
    }

    private static CurrentUserDto ToDto(AppUser u, Tenant t) =>
        new(u.Id, u.Email, u.FullName, u.Role.ToString(), t.Id, t.Name, t.StateCode);

    private async Task<string> UniqueSlugAsync(string companyName, CancellationToken ct)
    {
        var baseSlug = NonSlugChars().Replace(companyName.Trim().ToLowerInvariant(), "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "company";
        if (baseSlug.Length > 60) baseSlug = baseSlug[..60].TrimEnd('-');

        var slug = baseSlug;
        for (var i = 2; await db.Tenants.AnyAsync(t => t.Slug == slug, ct); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
