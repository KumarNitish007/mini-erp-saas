using MiniErp.Domain.Catalog;
using MiniErp.Domain.Common;

namespace MiniErp.Domain.Sales;

public enum InvoiceStatus
{
    Draft,
    Issued,
    Paid,
    Cancelled
}

/// <summary>
/// Invoice aggregate. Lifecycle: Draft → Issued → Paid, or Draft/Issued → Cancelled.
/// Only drafts are editable; issuing assigns the legal number and takes stock out.
/// </summary>
public sealed class Invoice : AuditableEntity, ITenantOwned
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice() { }

    public Invoice(Guid customerId, DateOnly invoiceDate, DateOnly dueDate, bool isInterState, string? notes)
    {
        CustomerId = customerId;
        Status = InvoiceStatus.Draft;
        SetHeader(invoiceDate, dueDate, isInterState, notes);
    }

    public Guid TenantId { get; set; }
    public string? Number { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public bool IsInterState { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal CgstAmount { get; private set; }
    public decimal SgstAmount { get; private set; }
    public decimal IgstAmount { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public bool IsOverdue(DateOnly today) => Status == InvoiceStatus.Issued && DueDate < today;

    public void SetHeader(DateOnly invoiceDate, DateOnly dueDate, bool isInterState, string? notes)
    {
        EnsureDraft();
        if (dueDate < invoiceDate) throw new DomainException("Due date cannot be before the invoice date.");
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        IsInterState = isInterState;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Recalculate();
    }

    public void ReplaceLines(IEnumerable<(Product Product, decimal Quantity, decimal? UnitPrice, string? Description)> lines)
    {
        EnsureDraft();
        _lines.Clear();
        var order = 0;
        foreach (var (product, quantity, unitPrice, description) in lines)
        {
            if (!product.IsActive) throw new DomainException($"Product {product.Sku} is inactive.");
            if (quantity <= 0) throw new DomainException("Line quantity must be greater than zero.");

            var price = unitPrice ?? product.UnitPrice;
            var calc = GstCalculator.CalculateLine(new GstLineInput(quantity, price, product.GstRate));
            _lines.Add(new InvoiceLine(Id, product.Id,
                string.IsNullOrWhiteSpace(description) ? product.Name : description.Trim(),
                quantity, price, product.GstRate, calc, order++));
        }
        Recalculate();
    }

    /// <summary>Issues the invoice: validates, assigns the number and returns the stock movements to persist.</summary>
    public IReadOnlyList<StockMovement> Issue(string number, IReadOnlyDictionary<Guid, Product> products, DateTime nowUtc)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new DomainException("Cannot issue an invoice without lines.");

        var movements = new List<StockMovement>();
        foreach (var group in _lines.GroupBy(l => l.ProductId))
        {
            var product = products[group.Key];
            movements.Add(product.RemoveStock(group.Sum(l => l.Quantity), StockMovementType.Sale, number));
        }

        Number = number;
        Status = InvoiceStatus.Issued;
        IssuedAtUtc = nowUtc;
        return movements;
    }

    public void MarkPaid(DateTime nowUtc)
    {
        if (Status != InvoiceStatus.Issued) throw new DomainException("Only issued invoices can be marked as paid.");
        Status = InvoiceStatus.Paid;
        PaidAtUtc = nowUtc;
    }

    /// <summary>Cancels the invoice. An issued invoice returns its stock; a paid one cannot be cancelled.</summary>
    public IReadOnlyList<StockMovement> Cancel(IReadOnlyDictionary<Guid, Product> products)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} invoice cannot be cancelled.");

        var movements = new List<StockMovement>();
        if (Status == InvoiceStatus.Issued)
        {
            foreach (var group in _lines.GroupBy(l => l.ProductId))
                movements.Add(products[group.Key].AddStock(group.Sum(l => l.Quantity), StockMovementType.SaleReversal, Number));
        }

        Status = InvoiceStatus.Cancelled;
        return movements;
    }

    private void Recalculate()
    {
        var totals = GstCalculator.CalculateTotals(
            _lines.Select(l => new GstLineResult(l.TaxableAmount, l.TaxAmount, l.LineTotal)), IsInterState);
        SubTotal = totals.SubTotal;
        CgstAmount = totals.Cgst;
        SgstAmount = totals.Sgst;
        IgstAmount = totals.Igst;
        GrandTotal = totals.GrandTotal;
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft) throw new DomainException("Only draft invoices can be changed.");
    }
}

public sealed class InvoiceLine : Entity
{
    private InvoiceLine() { }

    internal InvoiceLine(Guid invoiceId, Guid productId, string description, decimal quantity, decimal unitPrice,
        decimal gstRate, GstLineResult calc, int sortOrder)
    {
        InvoiceId = invoiceId;
        ProductId = productId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        GstRate = gstRate;
        TaxableAmount = calc.TaxableAmount;
        TaxAmount = calc.TaxAmount;
        LineTotal = calc.LineTotal;
        SortOrder = sortOrder;
    }

    public Guid InvoiceId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal GstRate { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public int SortOrder { get; private set; }
}
