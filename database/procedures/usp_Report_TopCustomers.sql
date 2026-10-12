/* Best customers by billed value since @FromDate. */
CREATE OR ALTER PROCEDURE dbo.usp_Report_TopCustomers
    @TenantId UNIQUEIDENTIFIER,
    @FromDate DATE,
    @Top      INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        c.Id,
        c.Name,
        InvoiceCount = COUNT(i.Id),
        TotalBilled  = SUM(i.GrandTotal),
        Outstanding  = ISNULL(SUM(CASE WHEN i.Status = N'Issued' THEN i.GrandTotal END), 0)
    FROM dbo.Invoices i
    JOIN dbo.Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
    WHERE i.TenantId = @TenantId
      AND i.Status IN (N'Issued', N'Paid')
      AND i.InvoiceDate >= @FromDate
    GROUP BY c.Id, c.Name
    ORDER BY TotalBilled DESC;
END
