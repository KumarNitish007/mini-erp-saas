using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Application.Features.Customers;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Sales;

namespace MiniErp.Application.Features.Invoices;

public sealed record InvoiceSummaryDto(
    Guid Id, string? Number, Guid CustomerId, string CustomerName, DateOnly InvoiceDate, DateOnly DueDate,
    string Status, bool IsOverdue, decimal GrandTotal);

public sealed record InvoiceLineDto(
    Guid Id, Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal GstRate,
    decimal TaxableAmount, decimal TaxAmount, decimal LineTotal);

public sealed record InvoiceDetailDto(
    Guid Id, string? Number, CustomerDto Customer, DateOnly InvoiceDate, DateOnly DueDate, string Status,
    bool IsOverdue, bool IsInterState, decimal SubTotal, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount,
    decimal GrandTotal, string? Notes, DateTime? IssuedAtUtc, DateTime? PaidAtUtc, IReadOnlyList<InvoiceLineDto> Lines);

public sealed record SaveInvoiceLineRequest(Guid ProductId, decimal Quantity, decimal? UnitPrice, string? Description);

public sealed record SaveInvoiceRequest(
    Guid CustomerId, DateOnly InvoiceDate, DateOnly DueDate, string? Notes, IReadOnlyList<SaveInvoiceLineRequest> Lines);

public sealed record InvoiceQuery : PagedQuery
{
    public string? Status { get; init; }
    public bool OverdueOnly { get; init; }
    public Guid? CustomerId { get; init; }
}

internal sealed class SaveInvoiceValidator : AbstractValidator<SaveInvoiceRequest>
{
    public SaveInvoiceValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate).WithMessage("Due date cannot be before the invoice date.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line.");
        RuleFor(x => x.Lines.Count).LessThanOrEqualTo(100).When(x => x.Lines is not null);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).PrecisionScale(18, 3, true);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).When(l => l.UnitPrice.HasValue);
            line.RuleFor(l => l.Description).MaximumLength(300);
        });
    }
}

public sealed class InvoiceService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IInvoiceNumberGenerator numbers,
    IInvoicePdfGenerator pdf,
    TimeProvider clock,
    IValidator<SaveInvoiceRequest> validator)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public Task<PagedResult<InvoiceSummaryDto>> ListAsync(InvoiceQuery query, CancellationToken ct)
    {
        var today = Today;
        var q = db.Invoices.AsNoTracking();

        if (Enum.TryParse<InvoiceStatus>(query.Status, true, out var status)) q = q.Where(i => i.Status == status);
        if (query.OverdueOnly) q = q.Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < today);
        if (query.CustomerId is { } customerId) q = q.Where(i => i.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(i => (i.Number != null && i.Number.Contains(s)) || i.Customer!.Name.Contains(s));
        }

        return q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvoiceSummaryDto(i.Id, i.Number, i.CustomerId, i.Customer!.Name, i.InvoiceDate, i.DueDate,
                i.Status.ToString(), i.Status == InvoiceStatus.Issued && i.DueDate < today, i.GrandTotal))
            .ToPagedAsync(query, ct);
    }

    public async Task<InvoiceDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        Map(await LoadAsync(id, ct, tracking: false));

    public async Task<InvoiceDetailDto> CreateDraftAsync(SaveInvoiceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var (customer, interState) = await ResolveCustomerAsync(request.CustomerId, ct);

        var invoice = new Invoice(customer.Id, request.InvoiceDate, request.DueDate, interState, request.Notes);
        invoice.ReplaceLines(await ResolveLinesAsync(request.Lines, ct));
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return await GetAsync(invoice.Id, ct);
    }

    public async Task<InvoiceDetailDto> UpdateDraftAsync(Guid id, SaveInvoiceRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var invoice = await LoadAsync(id, ct);
        if (invoice.CustomerId != request.CustomerId)
            throw new ConflictException("The customer of an invoice cannot be changed; create a new draft instead.");

        var (_, interState) = await ResolveCustomerAsync(request.CustomerId, ct);
        invoice.SetHeader(request.InvoiceDate, request.DueDate, interState, request.Notes);
        invoice.ReplaceLines(await ResolveLinesAsync(request.Lines, ct));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Issues a draft in one transaction: allocate the number (stored procedure), deduct stock and
    /// write the stock ledger. Row-version checks on products make concurrent sales of the same
    /// item fail fast instead of overselling.
    /// </summary>
    public async Task<InvoiceDetailDto> IssueAsync(Guid id, CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(async token =>
        {
            var invoice = await LoadAsync(id, token);
            if (invoice.Status != InvoiceStatus.Draft) throw new ConflictException("Only draft invoices can be issued.");

            var products = await LoadProductsAsync(invoice, token);
            var number = await numbers.NextAsync(currentUser.RequiredTenantId, invoice.InvoiceDate.Year, token);
            db.StockMovements.AddRange(invoice.Issue(number, products, clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<InvoiceDetailDto> MarkPaidAsync(Guid id, CancellationToken ct)
    {
        var invoice = await LoadAsync(id, ct);
        invoice.MarkPaid(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return Map(invoice);
    }

    public async Task<InvoiceDetailDto> CancelAsync(Guid id, CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(async token =>
        {
            var invoice = await LoadAsync(id, token);
            var products = await LoadProductsAsync(invoice, token);
            db.StockMovements.AddRange(invoice.Cancel(products));
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteDraftAsync(Guid id, CancellationToken ct)
    {
        var invoice = await LoadAsync(id, ct);
        if (invoice.Status != InvoiceStatus.Draft) throw new ConflictException("Only drafts can be deleted; cancel issued invoices instead.");
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Content, string FileName)> GetPdfAsync(Guid id, CancellationToken ct)
    {
        var invoice = await GetAsync(id, ct);
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == currentUser.RequiredTenantId, ct);
        var bytes = pdf.Generate(invoice, new InvoiceIssuer(tenant.Name, tenant.StateCode, tenant.Gstin, tenant.Address));
        return (bytes, $"{invoice.Number ?? "DRAFT-" + invoice.Id.ToString()[..8]}.pdf");
    }

    private async Task<Invoice> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        IQueryable<Invoice> q = db.Invoices.Include(i => i.Customer).Include(i => i.Lines);
        if (!tracking) q = q.AsNoTracking();
        return await q.SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(Invoice invoice, CancellationToken ct)
    {
        var ids = invoice.Lines.Select(l => l.ProductId).Distinct().ToList();
        return await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
    }

    private async Task<(Customer Customer, bool InterState)> ResolveCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException("Customer", customerId);
        if (!customer.IsActive) throw new ConflictException("The customer is inactive.");

        var tenantState = await db.Tenants.Where(t => t.Id == currentUser.RequiredTenantId).Select(t => t.StateCode).SingleAsync(ct);
        // Place of supply: different state from the seller → inter-state (IGST).
        return (customer, customer.StateCode != tenantState);
    }

    private async Task<List<(Product, decimal, decimal?, string?)>> ResolveLinesAsync(IReadOnlyList<SaveInvoiceLineRequest> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        return lines.Select(l => products.TryGetValue(l.ProductId, out var p)
                ? (p, l.Quantity, l.UnitPrice, l.Description)
                : throw new NotFoundException("Product", l.ProductId))
            .ToList();
    }

    private InvoiceDetailDto Map(Invoice i) => new(
        i.Id, i.Number, CustomerService.Map(i.Customer!), i.InvoiceDate, i.DueDate, i.Status.ToString(), i.IsOverdue(Today),
        i.IsInterState, i.SubTotal, i.CgstAmount, i.SgstAmount, i.IgstAmount, i.GrandTotal, i.Notes, i.IssuedAtUtc, i.PaidAtUtc,
        i.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLineDto(l.Id, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.GstRate,
                l.TaxableAmount, l.TaxAmount, l.LineTotal))
            .ToList());
}
