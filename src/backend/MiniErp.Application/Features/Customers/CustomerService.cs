using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Sales;

namespace MiniErp.Application.Features.Customers;

public sealed record CustomerDto(
    Guid Id, string Name, string? Email, string? Phone, string? Gstin, string StateCode, string StateName,
    string? BillingAddress, bool IsActive);

public sealed record SaveCustomerRequest(
    string Name, string StateCode, string? Email, string? Phone, string? Gstin, string? BillingAddress);

public sealed record CustomerQuery : PagedQuery
{
    public bool IncludeInactive { get; init; }
}

internal sealed class SaveCustomerValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StateCode).ValidStateCode();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Matches(@"^\+?[0-9 \-]{7,20}$").When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Gstin)
            .Must((req, gstin) => IndianStates.IsValidGstin(gstin?.Trim().ToUpperInvariant(), req.StateCode))
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin))
            .WithMessage("GSTIN is not valid for the selected state.");
        RuleFor(x => x.BillingAddress).MaximumLength(500);
    }
}

public sealed class CustomerService(IAppDbContext db, IValidator<SaveCustomerRequest> validator)
{
    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerQuery query, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking();
        if (!query.IncludeInactive) q = q.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => c.Name.Contains(s) || (c.Email != null && c.Email.Contains(s)) || (c.Gstin != null && c.Gstin.Contains(s)));
        }

        var page = await q.OrderBy(c => c.Name).ToPagedAsync(query, ct);
        return new PagedResult<CustomerDto>(page.Items.Select(Map).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct) => Map(await FindAsync(id, ct, tracking: false));

    public async Task<CustomerDto> CreateAsync(SaveCustomerRequest r, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var customer = new Customer(r.Name, r.StateCode, r.Email, r.Phone, r.Gstin, r.BillingAddress);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return Map(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, SaveCustomerRequest r, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(r, ct);
        var customer = await FindAsync(id, ct);
        customer.Update(r.Name, r.StateCode, r.Email, r.Phone, r.Gstin, r.BillingAddress);
        await db.SaveChangesAsync(ct);
        return Map(customer);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var customer = await FindAsync(id, ct);
        customer.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Customer> FindAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = tracking ? db.Customers : db.Customers.AsNoTracking();
        return await q.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Customer", id);
    }

    internal static CustomerDto Map(Customer c) =>
        new(c.Id, c.Name, c.Email, c.Phone, c.Gstin, c.StateCode, IndianStates.NameOf(c.StateCode), c.BillingAddress, c.IsActive);
}
