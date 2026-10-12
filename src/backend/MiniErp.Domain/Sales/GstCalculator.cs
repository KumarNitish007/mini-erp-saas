namespace MiniErp.Domain.Sales;

public sealed record GstLineInput(decimal Quantity, decimal UnitPrice, decimal GstRate);

public sealed record GstLineResult(decimal TaxableAmount, decimal TaxAmount, decimal LineTotal);

public sealed record GstTotals(decimal SubTotal, decimal Cgst, decimal Sgst, decimal Igst, decimal GrandTotal)
{
    public decimal TotalTax => Cgst + Sgst + Igst;
}

/// <summary>
/// Indian GST rules used by the invoice:
/// intra-state supply  → tax split equally into CGST + SGST;
/// inter-state supply  → the whole tax is IGST.
/// Every amount is rounded to paise per line, and SGST takes the rounding remainder
/// so CGST + SGST always equals the line-level tax exactly.
/// </summary>
public static class GstCalculator
{
    public static GstLineResult CalculateLine(GstLineInput line)
    {
        var taxable = Round(line.Quantity * line.UnitPrice);
        var tax = Round(taxable * line.GstRate / 100m);
        return new GstLineResult(taxable, tax, taxable + tax);
    }

    public static GstTotals CalculateTotals(IEnumerable<GstLineResult> lines, bool isInterState)
    {
        decimal subTotal = 0, tax = 0;
        foreach (var l in lines)
        {
            subTotal += l.TaxableAmount;
            tax += l.TaxAmount;
        }

        if (isInterState)
            return new GstTotals(subTotal, 0, 0, tax, subTotal + tax);

        var cgst = Round(tax / 2m);
        var sgst = tax - cgst;
        return new GstTotals(subTotal, cgst, sgst, 0, subTotal + tax);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
