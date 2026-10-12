namespace MiniErp.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>Marks an entity that belongs to exactly one tenant. The persistence layer
/// stamps <see cref="TenantId"/> on insert and filters every query by it.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

/// <summary>A business rule was violated (maps to HTTP 409/422 at the API edge).</summary>
public sealed class DomainException(string message) : Exception(message);
