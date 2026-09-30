using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface IPayrollRunRepository
{
    Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default);

    Task AddVersionAsync(PayrollRunVersion version, CancellationToken cancellationToken = default);

    Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

    Task<PayrollRunVersion?> GetVersionWithPaymentsAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<PayrollRunVersion?> GetLatestVersionWithPaymentsAsync(Guid payrollRunId, CancellationToken cancellationToken = default);
}
