/* Sales (issued + paid invoices) per month for the last @Months months,
   including months with zero sales so charts have no gaps. */
CREATE OR ALTER PROCEDURE dbo.usp_Report_MonthlySales
    @TenantId UNIQUEIDENTIFIER,
    @Today    DATE,
    @Months   INT = 6
AS
BEGIN
    SET NOCOUNT ON;

    IF @Months < 1 SET @Months = 1;
    IF @Months > 24 SET @Months = 24;

    DECLARE @FirstMonth DATE = DATEADD(MONTH, 1 - @Months, DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1));

    WITH Months AS
    (
        SELECT TOP (@Months)
               MonthStart = DATEADD(MONTH, CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT) - 1, @FirstMonth)
        FROM sys.all_objects
    )
    SELECT
        m.MonthStart,
        InvoiceCount = COUNT(i.Id),
        TaxableTotal = ISNULL(SUM(i.SubTotal), 0),
        TaxTotal     = ISNULL(SUM(i.CgstAmount + i.SgstAmount + i.IgstAmount), 0),
        GrandTotal   = ISNULL(SUM(i.GrandTotal), 0)
    FROM Months m
    LEFT JOIN dbo.Invoices i
           ON i.TenantId = @TenantId
          AND i.Status IN (N'Issued', N'Paid')
          AND i.InvoiceDate >= m.MonthStart
          AND i.InvoiceDate <  DATEADD(MONTH, 1, m.MonthStart)
    GROUP BY m.MonthStart
    ORDER BY m.MonthStart;
END
