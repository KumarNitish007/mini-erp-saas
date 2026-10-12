using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using MiniErp.Application.Common;
using MiniErp.Application.Features.Dashboard;

namespace MiniErp.Infrastructure.Reporting;

public sealed record ConnectionStrings(string Default);

/// <summary>Read-side queries: Dapper over the stored procedures in database/procedures.</summary>
internal sealed class SqlReportQueries(ConnectionStrings connectionStrings) : IReportQueries
{
    public async Task<DashboardKpis> GetKpisAsync(Guid tenantId, DateOnly today, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionStrings.Default);
        return await conn.QuerySingleAsync<DashboardKpis>(Proc("dbo.usp_Dashboard_Kpis",
            new { TenantId = tenantId, Today = AsDate(today) }, ct));
    }

    public async Task<IReadOnlyList<MonthlySalesPoint>> GetMonthlySalesAsync(Guid tenantId, DateOnly today, int months, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionStrings.Default);
        var rows = await conn.QueryAsync<MonthlySalesPoint>(Proc("dbo.usp_Report_MonthlySales",
            new { TenantId = tenantId, Today = AsDate(today), Months = months }, ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(Guid tenantId, int top, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionStrings.Default);
        var rows = await conn.QueryAsync<LowStockItem>(Proc("dbo.usp_Report_LowStock", new { TenantId = tenantId, Top = top }, ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<TopCustomer>> GetTopCustomersAsync(Guid tenantId, DateOnly fromDate, int top, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionStrings.Default);
        var rows = await conn.QueryAsync<TopCustomer>(Proc("dbo.usp_Report_TopCustomers",
            new { TenantId = tenantId, FromDate = AsDate(fromDate), Top = top }, ct));
        return rows.AsList();
    }

    private static CommandDefinition Proc(string name, object args, CancellationToken ct) =>
        new(name, args, commandType: CommandType.StoredProcedure, cancellationToken: ct);

    private static DateTime AsDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
}
