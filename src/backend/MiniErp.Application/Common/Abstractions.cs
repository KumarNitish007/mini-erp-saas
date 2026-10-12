using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Sales;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;

namespace MiniErp.Application.Common;

/// <summary>Unit of work over the tenant-scoped data model (implemented with EF Core).</summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Product> Products { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Invoice> Invoices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="operation"/> in a database transaction (retry-safe).</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>The authenticated caller, resolved from the JWT.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    UserRole? Role { get; }

    Guid RequiredTenantId => TenantId ?? throw new UnauthorizedAccessException("No tenant in the current context.");
}

public interface IPasswordHasher
{
    string Hash(AppUser user, string password);
    bool Verify(AppUser user, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateToken(AppUser user, Tenant tenant);
}

/// <summary>Allocates gap-free invoice numbers inside the current transaction.</summary>
public interface IInvoiceNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
}

/// <summary>Read-side reporting queries (Dapper + stored procedures).</summary>
public interface IReportQueries
{
    Task<DashboardKpis> GetKpisAsync(Guid tenantId, DateOnly today, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlySalesPoint>> GetMonthlySalesAsync(Guid tenantId, DateOnly today, int months, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(Guid tenantId, int top, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopCustomer>> GetTopCustomersAsync(Guid tenantId, DateOnly fromDate, int top, CancellationToken cancellationToken = default);
}

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoiceDetailDto invoice, InvoiceIssuer issuer);
}

public sealed record InvoiceIssuer(string Name, string StateCode, string? Gstin, string? Address);
