using MiniErp.Domain.Catalog;
using MiniErp.Domain.Common;
using Shouldly;

namespace MiniErp.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Sku_is_normalised_to_upper_case()
    {
        new Product(" pipe-25 ", "Pipe", "pcs", 10m, 18).Sku.ShouldBe("PIPE-25");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(30)]
    public void Only_real_gst_slabs_are_allowed(decimal rate)
    {
        Should.Throw<DomainException>(() => new Product("A", "A", "pcs", 10m, rate));
    }

    [Fact]
    public void Stock_movements_are_signed_and_keep_a_running_total()
    {
        var p = new Product("A", "A", "pcs", 10m, 18, reorderLevel: 5);

        p.AddStock(10, StockMovementType.Purchase, "PO-1").Quantity.ShouldBe(10);
        p.RemoveStock(6, StockMovementType.Adjustment, "Damaged").Quantity.ShouldBe(-6);

        p.StockQuantity.ShouldBe(4);
        p.IsLowStock.ShouldBeTrue();
    }

    [Fact]
    public void Stock_can_never_go_negative()
    {
        var p = new Product("A", "A", "pcs", 10m, 18);
        p.AddStock(1, StockMovementType.Opening, null);

        Should.Throw<DomainException>(() => p.RemoveStock(2, StockMovementType.Adjustment, null));
        p.StockQuantity.ShouldBe(1);
    }
}
