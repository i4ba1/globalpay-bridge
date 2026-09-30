-- GBP-DBD-001 §5: set-based matching of expected payments against confirmed provider results.
-- Returns one row per mismatch so the worker can open reconciliation cases.
CREATE OR ALTER PROCEDURE dbo.usp_ReconcilePaymentResults
    @TenantId  UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.Id                AS PaymentBatchId,
        i.Id                AS PaymentInstructionId,
        i.WorkerId,
        i.Amount            AS ExpectedAmount,
        i.Currency          AS CurrencyCode,
        i.Status            AS InstructionStatus,
        CASE
            WHEN i.Status = 4 THEN 'PaymentFailed'
            WHEN i.Status IN (1, 2) THEN 'MissingConfirmation'
            ELSE 'None'
        END                 AS Reason
    FROM dbo.PaymentInstruction AS i
    JOIN dbo.PaymentBatch AS b ON b.Id = i.BatchId
    WHERE i.TenantId = @TenantId
      AND b.Status IN (1, 2)
      AND i.Status IN (1, 2, 4)
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.ReconciliationCase AS c
            WHERE c.PaymentInstructionId = i.Id
              AND c.Status IN (0, 1))
    ORDER BY b.Id, i.WorkerId;
END;
