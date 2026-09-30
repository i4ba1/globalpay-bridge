using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Payroll;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class PayrollRunRepository : IPayrollRunRepository
{
    private readonly GlobalPayDbContext _context;

    public PayrollRunRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default) =>
        await _context.PayrollRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);

    public async Task AddVersionAsync(PayrollRunVersion version, CancellationToken cancellationToken = default) =>
        await _context.PayrollRunVersions.AddAsync(version, cancellationToken).ConfigureAwait(false);

    public Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
        _context.PayrollRuns.FirstOrDefaultAsync(run => run.Id == payrollRunId, cancellationToken);

    public Task<PayrollRunVersion?> GetVersionWithPaymentsAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _context.PayrollRunVersions
            .Include(version => version.Payments)
            .ThenInclude(payment => payment.Lines)
            .FirstOrDefaultAsync(version => version.Id == versionId, cancellationToken);

    public Task<PayrollRunVersion?> GetLatestVersionWithPaymentsAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default) =>
        _context.PayrollRunVersions
            .Include(version => version.Payments)
            .ThenInclude(payment => payment.Lines)
            .Where(version => version.PayrollRunId == payrollRunId)
            .OrderByDescending(version => version.Version)
            .FirstOrDefaultAsync(cancellationToken);
}
