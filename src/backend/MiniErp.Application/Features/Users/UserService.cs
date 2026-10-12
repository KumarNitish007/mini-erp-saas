using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Users;

namespace MiniErp.Application.Features.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAtUtc);

public sealed record CreateUserRequest(string Email, string FullName, string Role, string Password);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Role).Must(r => r is nameof(UserRole.Admin) or nameof(UserRole.Staff))
            .WithMessage("Role must be Admin or Staff.");
        RuleFor(x => x.Password).StrongPassword();
    }
}

/// <summary>Team management inside a tenant (Owner/Admin only — enforced by API policy).</summary>
public sealed class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, IValidator<CreateUserRequest> validator)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.Email, u.FullName, u.Role.ToString(), u.IsActive, u.CreatedAtUtc))
            .ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = new AppUser(currentUser.RequiredTenantId, email, request.FullName, Enum.Parse<UserRole>(request.Role));
        user.SetPasswordHash(hasher.Hash(user, request.Password));
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new UserDto(user.Id, user.Email, user.FullName, user.Role.ToString(), user.IsActive, user.CreatedAtUtc);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        if (id == currentUser.UserId) throw new ConflictException("You cannot deactivate your own account.");
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id);
        user.Deactivate();
        await db.SaveChangesAsync(ct);
    }
}
