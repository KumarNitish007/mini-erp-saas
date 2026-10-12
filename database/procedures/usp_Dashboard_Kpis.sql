/* Headline numbers for the dashboard, in a single round trip. */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Kpis
    @TenantId UNIQUEIDENTIFIER,
    @Today    DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MonthStart DATE = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);

    SELECT
        RevenueThisMonth  = ISNULL(SUM(CASE WHEN i.Status IN (N'Issued', N'Paid') AND i.InvoiceDate >= @MonthStart AND i.InvoiceDate <= @Today THEN i.GrandTotal END), 0),
        OutstandingAmount = ISNULL(SUM(CASE WHEN i.Status = N'Issued' THEN i.GrandTotal END), 0),
        OverdueAmount     = ISNULL(SUM(CASE WHEN i.Status = N'Issued' AND i.DueDate < @Today THEN i.GrandTotal END), 0),
        OverdueCount      = COUNT(CASE WHEN i.Status = N'Issued' AND i.DueDate < @Today THEN 1 END),
        DraftCount        = COUNT(CASE WHEN i.Status = N'Draft' THEN 1 END),
        LowStockCount     = (SELECT COUNT(*) FROM dbo.Products p
                              WHERE p.TenantId = @TenantId AND p.IsActive = 1 AND p.StockQuantity <= p.ReorderLevel),
        CustomerCount     = (SELECT COUNT(*) FROM dbo.Customers c
                              WHERE c.TenantId = @TenantId AND c.IsActive = 1)
    FROM dbo.Invoices i
    WHERE i.TenantId = @TenantId;
END
