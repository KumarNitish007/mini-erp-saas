using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common;
using MiniErp.Domain.Catalog;

namespace MiniErp.Application.Features.Products;

public sealed record ProductDto(
    Guid Id, string Sku, string Name, string Unit, string? HsnCode, decimal UnitPrice, decimal GstRate,
    decimal StockQuantity, decimal ReorderLevel, bool IsActive, bool IsLowStock);

public sealed record SaveProductRequest(
    string Sku, string Name, string Unit, string? HsnCode, decimal UnitPrice, decimal GstRate,
    decimal ReorderLevel, decimal OpeningStock = 0);

/// <summary>Positive quantity = stock in (purchase), negative = stock out (adjustment).</summary>
public sealed record StockAdjustmentRequest(decimal Quantity, string Type, string? Reference);

public sealed record StockMovementDto(Guid Id, decimal Quantity, string Type, string? Reference, DateTime CreatedAtUtc);

public sealed record ProductQuery : PagedQuery
{
    public bool IncludeInactive { get; init; }
    public bool LowStockOnly { get; init; }
}

internal sealed class SaveProductValidator : AbstractValidator<SaveProductRequest>
{
    public SaveProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("SKU may contain letters, digits, '.', '_' and '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.HsnCode).Matches("^[0-9]{4,8}$").When(x => !string.IsNullOrWhiteSpace(x.HsnCode))
            .WithMessage("HSN code must be 4 to 8 digits.");
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.GstRate).Must(Product.AllowedGstRates.Contains)
            .WithMessage("GST rate must be one of 0, 0.25, 3, 5, 12, 18 or 28.");
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OpeningStock).GreaterThanOrEqualTo(0);
    }
}

internal sealed class StockAdjustmentValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentValidator()
    {
        RuleFor(x => x.Quantity).NotEqual(0);
        RuleFor(x => x.Type).Must(t => t is nameof(StockMovementType.Purchase) or nameof(StockMovementType.Adjustment))
            .WithMessage("Type must be Purchase or Adjustment.");
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Type == nameof(StockMovementType.Purchase))
            .WithMessage("A purchase must add stock.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class ProductService(
    IAppDbContext db,
    IValidator<SaveProductRequest> saveValidator,
    IValidator<StockAdjustmentRequest> stockValidator)
{
    public Task<PagedResult<ProductDto>> ListAsync(ProductQuery query, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking();
        if (!query.IncludeInactive) q = q.Where(p => p.IsActive);
        if (query.LowStockOnly) q = q.Where(p => p.StockQuantity <= p.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(s) || p.Sku.Contains(s) || (p.HsnCode != null && p.HsnCode.StartsWith(s)));
        }

        return q.OrderBy(p => p.Name).Select(ToDto).ToPagedAsync(query, ct);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product", id);

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"A product with SKU '{sku}' already exists.");

        var product = new Product(sku, request.Name, request.Unit, request.UnitPrice, request.GstRate,
            request.ReorderLevel, request.HsnCode);
        db.Products.Add(product);
        if (request.OpeningStock > 0)
            db.StockMovements.Add(product.AddStock(request.OpeningStock, StockMovementType.Opening, "Opening balance"));

        await db.SaveChangesAsync(ct);
        return MapProduct(product);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);
        var product = await FindAsync(id, ct);
        if (!string.Equals(product.Sku, request.Sku.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("SKU cannot be changed once a product is created.");

        product.Update(request.Name, request.Unit, request.UnitPrice, request.GstRate, request.ReorderLevel, request.HsnCode);
        await db.SaveChangesAsync(ct);
        return MapProduct(product);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        if (active) product.Activate(); else product.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductDto> AdjustStockAsync(Guid id, StockAdjustmentRequest request, CancellationToken ct)
    {
        await stockValidator.ValidateAndThrowAsync(request, ct);
        var product = await FindAsync(id, ct);
        var type = Enum.Parse<StockMovementType>(request.Type);

        db.StockMovements.Add(request.Quantity > 0
            ? product.AddStock(request.Quantity, type, request.Reference)
            : product.RemoveStock(-request.Quantity, type, request.Reference));

        await db.SaveChangesAsync(ct);
        return MapProduct(product);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(Guid id, CancellationToken ct)
    {
        await FindAsync(id, ct);
        return await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(200)
            .Select(m => new StockMovementDto(m.Id, m.Quantity, m.Type.ToString(), m.Reference, m.CreatedAtUtc))
            .ToListAsync(ct);
    }

    private async Task<Product> FindAsync(Guid id, CancellationToken ct) =>
        await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product", id);

    private static readonly System.Linq.Expressions.Expression<Func<Product, ProductDto>> ToDto = p =>
        new ProductDto(p.Id, p.Sku, p.Name, p.Unit, p.HsnCode, p.UnitPrice, p.GstRate,
            p.StockQuantity, p.ReorderLevel, p.IsActive, p.StockQuantity <= p.ReorderLevel);

    private static readonly Func<Product, ProductDto> MapProduct = ToDto.Compile();
}
