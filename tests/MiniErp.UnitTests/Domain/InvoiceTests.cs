using MiniErp.Domain.Catalog;
using MiniErp.Domain.Common;
using MiniErp.Domain.Sales;
using Shouldly;

namespace MiniErp.UnitTests.Domain;

public class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);
    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Product Stocked(string sku, decimal price, decimal gst, decimal stock)
    {
        var p = new Product(sku, sku, "pcs", price, gst);
        p.AddStock(stock, StockMovementType.Opening, null);
        return p;
    }

    private static (Invoice Invoice, Dictionary<Guid, Product> Products) DraftWith(params (Product P, decimal Qty)[] lines)
    {
        var invoice = new Invoice(Guid.NewGuid(), Today, Today.AddDays(30), isInterState: false, notes: null);
        invoice.ReplaceLines(lines.Select(l => (l.P, l.Qty, (decimal?)null, (string?)null)));
        return (invoice, lines.Select(l => l.P).DistinctBy(p => p.Id).ToDictionary(p => p.Id));
    }

    [Fact]
    public void Draft_totals_follow_its_lines()
    {
        var (invoice, _) = DraftWith((Stocked("A", 100m, 18, 10), 2), (Stocked("B", 50m, 5, 10), 1));

        invoice.SubTotal.ShouldBe(250m);
        invoice.CgstAmount.ShouldBe(19.25m);
        invoice.SgstAmount.ShouldBe(19.25m);
        invoice.GrandTotal.ShouldBe(288.5m);
        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.Number.ShouldBeNull();
    }

    [Fact]
    public void Issuing_assigns_number_and_deducts_stock_per_product()
    {
        var product = Stocked("A", 100m, 18, 10);
        var (invoice, products) = DraftWith((product, 3), (product, 2));

        var movements = invoice.Issue("INV-2026-00001", products, Now);

        invoice.Status.ShouldBe(InvoiceStatus.Issued);
        invoice.Number.ShouldBe("INV-2026-00001");
        product.StockQuantity.ShouldBe(5);
        movements.ShouldHaveSingleItem().Quantity.ShouldBe(-5);
    }

    [Fact]
    public void Cannot_issue_more_than_available_stock()
    {
        var (invoice, products) = DraftWith((Stocked("A", 100m, 18, 1), 2));

        Should.Throw<DomainException>(() => invoice.Issue("INV-1", products, Now)).Message.ShouldContain("Insufficient stock");
        invoice.Status.ShouldBe(InvoiceStatus.Draft);
    }

    [Fact]
    public void Issued_invoice_is_read_only()
    {
        var (invoice, products) = DraftWith((Stocked("A", 100m, 18, 10), 1));
        invoice.Issue("INV-1", products, Now);

        Should.Throw<DomainException>(() => invoice.SetHeader(Today, Today, false, null));
    }

    [Fact]
    public void Cancelling_an_issued_invoice_returns_stock()
    {
        var product = Stocked("A", 100m, 18, 10);
        var (invoice, products) = DraftWith((product, 4));
        invoice.Issue("INV-1", products, Now);

        var movements = invoice.Cancel(products);

        invoice.Status.ShouldBe(InvoiceStatus.Cancelled);
        product.StockQuantity.ShouldBe(10);
        movements.ShouldHaveSingleItem().Type.ShouldBe(StockMovementType.SaleReversal);
    }

    [Fact]
    public void Paid_invoice_cannot_be_cancelled()
    {
        var (invoice, products) = DraftWith((Stocked("A", 100m, 18, 10), 1));
        invoice.Issue("INV-1", products, Now);
        invoice.MarkPaid(Now);

        Should.Throw<DomainException>(() => invoice.Cancel(products));
    }

    [Fact]
    public void Overdue_only_applies_to_unpaid_issued_invoices()
    {
        var (invoice, products) = DraftWith((Stocked("A", 100m, 18, 10), 1));
        invoice.IsOverdue(Today.AddDays(31)).ShouldBeFalse(); // draft

        invoice.Issue("INV-1", products, Now);
        invoice.IsOverdue(Today.AddDays(30)).ShouldBeFalse();
        invoice.IsOverdue(Today.AddDays(31)).ShouldBeTrue();

        invoice.MarkPaid(Now);
        invoice.IsOverdue(Today.AddDays(31)).ShouldBeFalse();
    }

    [Fact]
    public void Due_date_before_invoice_date_is_rejected()
    {
        Should.Throw<DomainException>(() => new Invoice(Guid.NewGuid(), Today, Today.AddDays(-1), false, null));
    }

    [Fact]
    public void Inactive_products_cannot_be_invoiced()
    {
        var product = Stocked("A", 100m, 18, 10);
        product.Deactivate();

        Should.Throw<DomainException>(() => DraftWith((product, 1)));
    }
}
