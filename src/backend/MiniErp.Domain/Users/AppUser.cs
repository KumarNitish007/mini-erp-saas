using MiniErp.Domain.Common;

namespace MiniErp.Domain.Users;

public enum UserRole
{
    Owner,
    Admin,
    Staff
}

public sealed class AppUser : AuditableEntity, ITenantOwned
{
    private AppUser() { }

    public AppUser(Guid tenantId, string email, string fullName, UserRole role)
    {
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        Role = role;
        IsActive = true;
    }

    public Guid TenantId { get; set; }
    public string Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public void Deactivate()
    {
        if (Role == UserRole.Owner)
            throw new DomainException("The tenant owner cannot be deactivated.");
        IsActive = false;
    }
}
