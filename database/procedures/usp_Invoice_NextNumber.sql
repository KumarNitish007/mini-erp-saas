/* Returns the next invoice sequence number for a tenant + year.
   Called inside the "issue invoice" transaction, so the number is only
   consumed if the invoice is actually issued (no gaps on rollback).
   UPDLOCK + HOLDLOCK serialises concurrent callers on the same key. */
CREATE OR ALTER PROCEDURE dbo.usp_Invoice_NextNumber
    @TenantId UNIQUEIDENTIFIER,
    @Year     INT,
    @Next     INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.InvoiceSequences WITH (UPDLOCK, HOLDLOCK)
       SET @Next = LastNumber = LastNumber + 1
     WHERE TenantId = @TenantId AND [Year] = @Year;

    IF @@ROWCOUNT = 0
    BEGIN
        BEGIN TRY
            INSERT dbo.InvoiceSequences (TenantId, [Year], LastNumber) VALUES (@TenantId, @Year, 1);
            SET @Next = 1;
        END TRY
        BEGIN CATCH
            -- Another session created the row first: fall back to incrementing it.
            IF ERROR_NUMBER() IN (2601, 2627)
                UPDATE dbo.InvoiceSequences
                   SET @Next = LastNumber = LastNumber + 1
                 WHERE TenantId = @TenantId AND [Year] = @Year;
            ELSE
                THROW;
        END CATCH
    END
END
