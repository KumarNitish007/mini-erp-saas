using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Application.Common;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>Calls dbo.usp_Invoice_NextNumber on the EF connection, enlisted in the current transaction.</summary>
internal sealed class SqlInvoiceNumberGenerator(AppDbContext db) : IInvoiceNumberGenerator
{
    public async Task<string> NextAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction()
                          ?? throw new InvalidOperationException("Invoice numbers must be allocated inside a transaction.");

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("Year", year);
        parameters.Add("Next", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Invoice_NextNumber", parameters, transaction,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return $"INV-{year}-{parameters.Get<int>("Next"):D5}";
    }
}
