using MiniErp.Domain.Common;

namespace MiniErp.Domain.Sales;

public sealed class Customer : AuditableEntity, ITenantOwned
{
    private Customer() { }

    public Customer(string name, string stateCode, string? email = null, string? phone = null,
        string? gstin = null, string? billingAddress = null)
    {
        Update(name, stateCode, email, phone, gstin, billingAddress);
        IsActive = true;
    }

    public Guid TenantId { get; set; }
    public string Name { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Gstin { get; private set; }
    public string StateCode { get; private set; } = null!;
    public string? BillingAddress { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string stateCode, string? email, string? phone, string? gstin, string? billingAddress)
    {
        Name = name.Trim();
        StateCode = stateCode;
        Email = Clean(email)?.ToLowerInvariant();
        Phone = Clean(phone);
        Gstin = Clean(gstin)?.ToUpperInvariant();
        BillingAddress = Clean(billingAddress);
    }

    public void Deactivate() => IsActive = false;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
