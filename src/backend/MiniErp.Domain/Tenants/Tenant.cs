using MiniErp.Domain.Common;

namespace MiniErp.Domain.Tenants;

public sealed class Tenant : Entity
{
    private Tenant() { }

    public Tenant(string name, string slug, string stateCode, string? gstin = null, string? address = null)
    {
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        StateCode = stateCode;
        Gstin = gstin;
        Address = address;
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;

    /// <summary>Two-digit GST state code (e.g. 27 = Maharashtra). Drives CGST/SGST vs IGST.</summary>
    public string StateCode { get; private set; } = null!;
    public string? Gstin { get; private set; }
    public string? Address { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
}
