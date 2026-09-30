-- GBP-DBD-001 §5: prior-period and per-worker variance report.
-- Set-based reporting path; the API uses an equivalent EF projection for provider portability.
CREATE OR ALTER PROCEDURE dbo.usp_GetPayrollVariance
    @TenantId      UNIQUEIDENTIFIER,
    @PayrollRunId  UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PeriodEnd DATE = (SELECT PeriodEnd FROM dbo.PayrollRun WHERE Id = @PayrollRunId AND TenantId = @TenantId);
    DECLARE @CurrentVersionId UNIQUEIDENTIFIER = (
        SELECT TOP (1) v.Id
        FROM dbo.PayrollRunVersion AS v
        WHERE v.PayrollRunId = @PayrollRunId AND v.TenantId = @TenantId
        ORDER BY v.Version DESC);

    IF @PeriodEnd IS NULL OR @CurrentVersionId IS NULL
    BEGIN
        RETURN;
    END

    DECLARE @PreviousVersionId UNIQUEIDENTIFIER = (
        SELECT TOP (1) v.Id
        FROM dbo.PayrollRun AS r
        JOIN dbo.PayrollRunVersion AS v ON v.PayrollRunId = r.Id
        WHERE r.TenantId = @TenantId
          AND r.PeriodEnd < @PeriodEnd
        ORDER BY r.PeriodEnd DESC, v.Version DESC);

    ;WITH CurrentPayments AS (
        SELECT p.WorkerId, p.Net
        FROM dbo.WorkerPayment AS p
        WHERE p.RunVersionId = @CurrentVersionId AND p.TenantId = @TenantId
    ),
    PreviousPayments AS (
        SELECT p.WorkerId, p.Net
        FROM dbo.WorkerPayment AS p
        WHERE p.RunVersionId = @PreviousVersionId AND p.TenantId = @TenantId
    ),
    Workers AS (
        SELECT WorkerId FROM CurrentPayments
        UNION
        SELECT WorkerId FROM PreviousPayments
    )
    SELECT
        w.WorkerId,
        worker.ExternalRef                        AS WorkerExternalRef,
        worker.DisplayName                        AS WorkerName,
        ISNULL(c.Net, 0)                          AS CurrentNet,
        ISNULL(p.Net, 0)                          AS PreviousNet,
        ISNULL(c.Net, 0) - ISNULL(p.Net, 0)       AS Variance,
        CASE
            WHEN ISNULL(p.Net, 0) = 0 THEN CASE WHEN ISNULL(c.Net, 0) = 0 THEN 0 ELSE 100 END
            ELSE CAST(ROUND((ISNULL(c.Net, 0) - ISNULL(p.Net, 0)) / p.Net * 100.0, 2) AS DECIMAL(19, 2))
        END                                       AS VariancePercent
    FROM Workers AS w
    LEFT JOIN CurrentPayments  AS c      ON c.WorkerId = w.WorkerId
    LEFT JOIN PreviousPayments AS p      ON p.WorkerId = w.WorkerId
    JOIN dbo.Worker AS worker            ON worker.Id = w.WorkerId AND worker.TenantId = @TenantId
    ORDER BY w.WorkerId;
END;
