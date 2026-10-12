using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniErp.Api.Infrastructure;
using MiniErp.Application.Common;
using MiniErp.Application.Features.Auth;
using MiniErp.Application.Features.Customers;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;
using MiniErp.Application.Features.Users;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Sign up a new company (tenant) with its owner account.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    public Task<AuthResponse> Register(RegisterTenantRequest request, CancellationToken ct) => auth.RegisterTenantAsync(request, ct);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(Policies.AuthRateLimit)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpGet("me")]
    public Task<CurrentUserDto> Me(CancellationToken ct) => auth.GetCurrentAsync(ct);
}

[ApiController]
[Route("api/lookups")]
public sealed class LookupsController : ControllerBase
{
    [HttpGet("states"), AllowAnonymous, ResponseCache(Duration = 86400)]
    public IReadOnlyList<StateDto> States() => IndianStates.All;
}

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Manager)]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(request, ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await users.DeactivateAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List([FromQuery] ProductQuery query, CancellationToken ct) => products.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request, CancellationToken ct)
    {
        var created = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, SaveProductRequest request, CancellationToken ct) => products.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/stock")]
    public Task<ProductDto> AdjustStock(Guid id, StockAdjustmentRequest request, CancellationToken ct) => products.AdjustStockAsync(id, request, ct);

    [HttpGet("{id:guid}/movements")]
    public Task<IReadOnlyList<StockMovementDto>> Movements(Guid id, CancellationToken ct) => products.GetMovementsAsync(id, ct);

    [HttpPost("{id:guid}/deactivate"), Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await products.SetActiveAsync(id, false, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate"), Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await products.SetActiveAsync(id, true, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerDto>> List([FromQuery] CustomerQuery query, CancellationToken ct) => customers.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<CustomerDto> Get(Guid id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(SaveCustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<CustomerDto> Update(Guid id, SaveCustomerRequest request, CancellationToken ct) => customers.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/deactivate"), Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await customers.DeactivateAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController(InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceSummaryDto>> List([FromQuery] InvoiceQuery query, CancellationToken ct) => invoices.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<InvoiceDetailDto> Get(Guid id, CancellationToken ct) => invoices.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<InvoiceDetailDto>> CreateDraft(SaveInvoiceRequest request, CancellationToken ct)
    {
        var created = await invoices.CreateDraftAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<InvoiceDetailDto> UpdateDraft(Guid id, SaveInvoiceRequest request, CancellationToken ct) => invoices.UpdateDraftAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct)
    {
        await invoices.DeleteDraftAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/issue")]
    public Task<InvoiceDetailDto> Issue(Guid id, CancellationToken ct) => invoices.IssueAsync(id, ct);

    [HttpPost("{id:guid}/mark-paid")]
    public Task<InvoiceDetailDto> MarkPaid(Guid id, CancellationToken ct) => invoices.MarkPaidAsync(id, ct);

    [HttpPost("{id:guid}/cancel"), Authorize(Policy = Policies.Manager)]
    public Task<InvoiceDetailDto> Cancel(Guid id, CancellationToken ct) => invoices.CancelAsync(id, ct);

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var (content, fileName) = await invoices.GetPdfAsync(id, ct);
        return File(content, "application/pdf", fileName);
    }
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] int months = 6, CancellationToken ct = default) => dashboard.GetAsync(months, ct);
}
