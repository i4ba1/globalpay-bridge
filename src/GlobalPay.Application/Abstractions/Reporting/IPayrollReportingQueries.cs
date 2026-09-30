namespace GlobalPay.Application.Abstractions.Reporting;

public sealed record PayrollVarianceLine(
    Guid WorkerId,
    string WorkerExternalRef,
    string WorkerName,
    decimal CurrentNet,
    decimal PreviousNet,
    decimal Variance,
    decimal VariancePercent);

public sealed record PayrollVarianceReport(
    Guid PayrollRunId,
    int Version,
    string CurrencyCode,
    decimal CurrentNetTotal,
    decimal PreviousNetTotal,
    decimal VarianceTotal,
    IReadOnlyList<PayrollVarianceLine> Lines);

public sealed record MonthlyPayrollSummary(
    DateOnly PeriodEnd,
    string CurrencyCode,
    int WorkerCount,
    decimal GrossTotal,
    decimal DeductionTotal,
    decimal NetTotal);

public interface IPayrollReportingQueries
{
    Task<PayrollVarianceReport?> GetVarianceAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonthlyPayrollSummary>> GetMonthlySummaryAsync(
        DateOnly fromPeriodEnd,
        DateOnly toPeriodEnd,
        CancellationToken cancellationToken = default);
}
