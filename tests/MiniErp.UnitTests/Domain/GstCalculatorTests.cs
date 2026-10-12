using MiniErp.Domain.Sales;
using Shouldly;

namespace MiniErp.UnitTests.Domain;

public class GstCalculatorTests
{
    [Fact]
    public void Line_tax_is_rounded_to_paise()
    {
        var line = GstCalculator.CalculateLine(new GstLineInput(Quantity: 3, UnitPrice: 33.33m, GstRate: 18));

        line.TaxableAmount.ShouldBe(99.99m);
        line.TaxAmount.ShouldBe(18.00m); // 17.9982 → 18.00
        line.LineTotal.ShouldBe(117.99m);
    }

    [Fact]
    public void Intra_state_supply_splits_tax_into_cgst_and_sgst()
    {
        var lines = new[] { GstCalculator.CalculateLine(new(1, 1000m, 18)) };

        var totals = GstCalculator.CalculateTotals(lines, isInterState: false);

        totals.Cgst.ShouldBe(90m);
        totals.Sgst.ShouldBe(90m);
        totals.Igst.ShouldBe(0m);
        totals.GrandTotal.ShouldBe(1180m);
    }

    [Fact]
    public void Inter_state_supply_charges_igst_only()
    {
        var lines = new[] { GstCalculator.CalculateLine(new(2, 500m, 12)) };

        var totals = GstCalculator.CalculateTotals(lines, isInterState: true);

        totals.Igst.ShouldBe(120m);
        totals.Cgst.ShouldBe(0m);
        totals.Sgst.ShouldBe(0m);
        totals.GrandTotal.ShouldBe(1120m);
    }

    [Fact]
    public void Odd_paise_go_to_sgst_so_halves_always_reconcile()
    {
        // tax = 0.05 → CGST 0.03 (away from zero), SGST 0.02
        var lines = new[] { GstCalculator.CalculateLine(new(1, 1m, 5)) };

        var totals = GstCalculator.CalculateTotals(lines, isInterState: false);

        (totals.Cgst + totals.Sgst).ShouldBe(totals.TotalTax);
        totals.TotalTax.ShouldBe(0.05m);
    }
}
