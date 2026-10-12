using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Sales;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;

namespace MiniErp.Infrastructure.Persistence;

// Mappings mirror database/migrations/*.sql. Keys are generated in the domain (Guid.NewGuid),
// so they are marked ValueGeneratedNever - new child rows are then always INSERTed.

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(80);
        b.Property(x => x.Gstin).HasMaxLength(15);
        b.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(x => x.Address).HasMaxLength(500);
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("Users");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.FullName).HasMaxLength(150);
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Sku).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Unit).HasMaxLength(20);
        b.Property(x => x.HsnCode).HasMaxLength(10);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.GstRate).HasPrecision(5, 2);
        b.Property(x => x.StockQuantity).HasPrecision(18, 3);
        b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.IsLowStock);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Gstin).HasMaxLength(15);
        b.Property(x => x.StateCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        b.Property(x => x.BillingAddress).HasMaxLength(500);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(1000);
        foreach (var money in new[] { nameof(Invoice.SubTotal), nameof(Invoice.CgstAmount), nameof(Invoice.SgstAmount), nameof(Invoice.IgstAmount), nameof(Invoice.GrandTotal) })
            b.Property(money).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("InvoiceLines");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.GstRate).HasPrecision(5, 2);
        b.Property(x => x.TaxableAmount).HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId);
    }
}
