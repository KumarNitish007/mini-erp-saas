using MiniErp.Domain.Common;

namespace MiniErp.Domain.Catalog;

public enum StockMovementType
{
    Opening,
    Purchase,
    Sale,
    SaleReversal,
    Adjustment
}

/// <summary>Append-only ledger of every stock change; Product.StockQuantity is its running total.</summary>
public sealed class StockMovement : Entity, ITenantOwned
{
    private StockMovement() { }

    internal StockMovement(Guid productId, decimal quantity, StockMovementType type, string? reference)
    {
        ProductId = productId;
        Quantity = quantity;
        Type = type;
        Reference = reference;
    }

    public Guid TenantId { get; set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public StockMovementType Type { get; private set; }
    public string? Reference { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
}
