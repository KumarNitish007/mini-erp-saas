/* Active products at or below their reorder level, most urgent first. */
CREATE OR ALTER PROCEDURE dbo.usp_Report_LowStock
    @TenantId UNIQUEIDENTIFIER,
    @Top      INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        p.Id,
        p.Sku,
        p.Name,
        p.Unit,
        p.StockQuantity,
        p.ReorderLevel,
        Shortfall = p.ReorderLevel - p.StockQuantity
    FROM dbo.Products p
    WHERE p.TenantId = @TenantId
      AND p.IsActive = 1
      AND p.StockQuantity <= p.ReorderLevel
    ORDER BY (p.ReorderLevel - p.StockQuantity) DESC, p.Name;
END
