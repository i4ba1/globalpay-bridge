-- GBP-DBD-001 §5: tenant and period aggregates for operational reporting.
CREATE OR ALTER PROCEDURE dbo.usp_GetMonthlyPayrollSummary
    @TenantId       UNIQUEIDENTIFIER,
    @FromPeriodEnd  DATE,
    @ToPeriodEnd    DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.PeriodEnd,
        v.Currency                AS CurrencyCode,
        v.WorkerCount,
        v.GrossTotal,
        v.DeductionTotal,
        v.NetTotal
    FROM dbo.PayrollRun AS r
    JOIN dbo.PayrollRunVersion AS v ON v.Id = r.CurrentVersionId
    WHERE r.TenantId = @TenantId
      AND r.CurrentVersionId IS NOT NULL
      AND r.PeriodEnd >= @FromPeriodEnd
      AND r.PeriodEnd <= @ToPeriodEnd
    ORDER BY r.PeriodEnd;
END;
