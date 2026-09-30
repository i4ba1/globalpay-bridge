using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Reporting;
using GlobalPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Reporting;

internal sealed class PayrollReportingQueries : IPayrollReportingQueries
{
    private readonly GlobalPayDbContext _context;
    private readonly ITenantContext _tenantContext;

    public PayrollReportingQueries(GlobalPayDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<PayrollVarianceReport?> GetVarianceAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return null;
        }

        var run = await _context.PayrollRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == payrollRunId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        var version = await _context.PayrollRunVersions
            .AsNoTracking()
            .Where(candidate => candidate.PayrollRunId == payrollRunId)
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return null;
        }

        var currentPayments = await GetPaymentsAsync(version.Id, cancellationToken).ConfigureAwait(false);

        var previousRun = await _context.PayrollRuns
            .AsNoTracking()
            .Where(candidate => candidate.PeriodEnd < run.PeriodEnd)
            .OrderByDescending(candidate => candidate.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var previousPayments = new Dictionary<Guid, decimal>();

        if (previousRun is not null)
        {
            var previousVersion = await _context.PayrollRunVersions
                .AsNoTracking()
                .Where(candidate => candidate.PayrollRunId == previousRun.Id)
                .OrderByDescending(candidate => candidate.Version)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (previousVersion is not null)
            {
                previousPayments = await GetPaymentsAsync(previousVersion.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        var workerIds = currentPayments.Keys
            .Union(previousPayments.Keys)
            .OrderBy(workerId => workerId)
            .ToArray();

        var workers = await _context.Workers
            .AsNoTracking()
            .Where(worker => workerIds.Contains(worker.Id))
            .Select(worker => new { worker.Id, worker.ExternalRef, worker.DisplayName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lines = new List<PayrollVarianceLine>(workerIds.Length);
        var currentTotal = 0m;
        var previousTotal = 0m;

        foreach (var workerId in workerIds)
        {
            currentPayments.TryGetValue(workerId, out var current);
            previousPayments.TryGetValue(workerId, out var previous);

            currentTotal += current;
            previousTotal += previous;

            var worker = workers.FirstOrDefault(candidate => candidate.Id == workerId);
            var variance = current - previous;
            var variancePercent = previous == 0m
                ? current == 0m ? 0m : 100m
                : Math.Round(variance / previous * 100m, 2);

            lines.Add(new PayrollVarianceLine(
                workerId,
                worker?.ExternalRef ?? string.Empty,
                worker?.DisplayName ?? string.Empty,
                current,
                previous,
                variance,
                variancePercent));
        }

        return new PayrollVarianceReport(
            payrollRunId,
            version.Version,
            run.Currency.Code,
            currentTotal,
            previousTotal,
            currentTotal - previousTotal,
            lines);
    }

    public async Task<IReadOnlyList<MonthlyPayrollSummary>> GetMonthlySummaryAsync(
        DateOnly fromPeriodEnd,
        DateOnly toPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return [];
        }

        return await _context.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CurrentVersionId != null
                          && run.PeriodEnd >= fromPeriodEnd
                          && run.PeriodEnd <= toPeriodEnd)
            .Join(
                _context.PayrollRunVersions.AsNoTracking(),
                run => run.CurrentVersionId,
                version => version.Id,
                (run, version) => new { run.PeriodEnd, version.Currency, version.WorkerCount, version.GrossTotal, version.DeductionTotal, version.NetTotal })
            .OrderBy(row => row.PeriodEnd)
            .Select(row => new MonthlyPayrollSummary(
                row.PeriodEnd,
                row.Currency.Code,
                row.WorkerCount,
                row.GrossTotal,
                row.DeductionTotal,
                row.NetTotal))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, decimal>> GetPaymentsAsync(
        Guid runVersionId,
        CancellationToken cancellationToken)
    {
        var payments = await _context.WorkerPayments
            .AsNoTracking()
            .Where(payment => payment.RunVersionId == runVersionId)
            .Select(payment => new { payment.WorkerId, payment.Net })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return payments.ToDictionary(payment => payment.WorkerId, payment => payment.Net);
    }
}
