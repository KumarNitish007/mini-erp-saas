using MiniErp.Domain.Common;

namespace MiniErp.Domain.Catalog;

public sealed class Product : AuditableEntity, ITenantOwned
{
    /// <summary>GST slabs accepted by the schema (CK_Products_GstRate).</summary>
    public static readonly IReadOnlySet<decimal> AllowedGstRates = new HashSet<decimal> { 0m, 0.25m, 3m, 5m, 12m, 18m, 28m };

    private Product() { }

    public Product(string sku, string name, string unit, decimal unitPrice, decimal gstRate,
        decimal reorderLevel = 0, string? hsnCode = null)
    {
        Sku = sku.Trim().ToUpperInvariant();
        Update(name, unit, unitPrice, gstRate, reorderLevel, hsnCode);
        IsActive = true;
    }

    public Guid TenantId { get; set; }
    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Unit { get; private set; } = null!;
    public string? HsnCode { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal GstRate { get; private set; }
    public decimal StockQuantity { get; private set; }
    public decimal ReorderLevel { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsLowStock => StockQuantity <= ReorderLevel;

    public void Update(string name, string unit, decimal unitPrice, decimal gstRate, decimal reorderLevel, string? hsnCode)
    {
        if (unitPrice < 0) throw new DomainException("Unit price cannot be negative.");
        if (!AllowedGstRates.Contains(gstRate)) throw new DomainException($"GST rate {gstRate}% is not a valid slab.");
        if (reorderLevel < 0) throw new DomainException("Reorder level cannot be negative.");

        Name = name.Trim();
        Unit = unit.Trim();
        UnitPrice = unitPrice;
        GstRate = gstRate;
        ReorderLevel = reorderLevel;
        HsnCode = string.IsNullOrWhiteSpace(hsnCode) ? null : hsnCode.Trim();
    }

    public StockMovement AddStock(decimal quantity, StockMovementType type, string? reference)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be greater than zero.");
        StockQuantity += quantity;
        return new StockMovement(Id, quantity, type, reference);
    }

    public StockMovement RemoveStock(decimal quantity, StockMovementType type, string? reference)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be greater than zero.");
        if (quantity > StockQuantity)
            throw new DomainException($"Insufficient stock for {Sku}: available {StockQuantity:0.###}, requested {quantity:0.###}.");
        StockQuantity -= quantity;
        return new StockMovement(Id, -quantity, type, reference);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
