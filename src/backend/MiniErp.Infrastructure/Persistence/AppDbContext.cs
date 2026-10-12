using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Common;
using MiniErp.Domain.Sales;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work. Tenant isolation is enforced here, in one place:
/// a global query filter on every <see cref="ITenantOwned"/> entity, plus stamping/guarding
/// TenantId on save so one tenant can never write rows into another.
/// The schema itself is owned by the SQL scripts in /database (applied with DbUp), not EF migrations.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, TimeProvider clock)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Evaluated per query; Guid.Empty (anonymous) matches nothing, so the default is deny.</summary>
    private Guid CurrentTenantId => currentUser.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Invoice>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            // A retried attempt must not see entities tracked by the failed one.
            ChangeTracker.Clear();
            await using var tx = await Database.BeginTransactionAsync(ct);
            var result = await operation(ct);
            await tx.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    private void ApplyTenantAndAuditRules()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var tenantId = currentUser.TenantId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantOwned owned)
            {
                if (entry.State == EntityState.Added)
                {
                    if (owned.TenantId == Guid.Empty)
                        owned.TenantId = tenantId ?? throw new InvalidOperationException(
                            $"Cannot save {entry.Metadata.ClrType.Name} without a tenant context.");
                    else if (tenantId is not null && owned.TenantId != tenantId)
                        throw new InvalidOperationException("Cross-tenant write blocked.");
                }
                else if (entry.State == EntityState.Modified && entry.Property(nameof(ITenantOwned.TenantId)).IsModified)
                {
                    throw new InvalidOperationException("TenantId is immutable.");
                }
            }

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAtUtc") is not null)
                entry.Property("CreatedAtUtc").CurrentValue = now;
            else if (entry.State == EntityState.Modified && entry.Entity is AuditableEntity auditable)
                auditable.UpdatedAtUtc = now;
        }
    }
}
