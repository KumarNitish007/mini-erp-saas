using MiniErp.Application.Common;

namespace MiniErp.Application.Features.Dashboard;

public sealed record DashboardKpis(
    decimal RevenueThisMonth, decimal OutstandingAmount, decimal OverdueAmount, int OverdueCount,
    int DraftCount, int LowStockCount, int CustomerCount);

public sealed record MonthlySalesPoint(DateTime MonthStart, int InvoiceCount, decimal TaxableTotal, decimal TaxTotal, decimal GrandTotal);

public sealed record LowStockItem(Guid Id, string Sku, string Name, string Unit, decimal StockQuantity, decimal ReorderLevel, decimal Shortfall);

public sealed record TopCustomer(Guid Id, string Name, int InvoiceCount, decimal TotalBilled, decimal Outstanding);

public sealed record DashboardDto(
    DashboardKpis Kpis,
    IReadOnlyList<MonthlySalesPoint> MonthlySales,
    IReadOnlyList<LowStockItem> LowStock,
    IReadOnlyList<TopCustomer> TopCustomers);

public sealed class DashboardService(IReportQueries reports, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<DashboardDto> GetAsync(int months, CancellationToken ct)
    {
        var tenantId = currentUser.RequiredTenantId;
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        months = Math.Clamp(months, 1, 24);

        // Sequential on purpose: the queries share one scoped connection factory and are each sub-millisecond.
        var kpis = await reports.GetKpisAsync(tenantId, today, ct);
        var sales = await reports.GetMonthlySalesAsync(tenantId, today, months, ct);
        var lowStock = await reports.GetLowStockAsync(tenantId, 8, ct);
        var top = await reports.GetTopCustomersAsync(tenantId, today.AddMonths(-months + 1).AddDays(1 - today.Day), 5, ct);

        return new DashboardDto(kpis, sales, lowStock, top);
    }
}
