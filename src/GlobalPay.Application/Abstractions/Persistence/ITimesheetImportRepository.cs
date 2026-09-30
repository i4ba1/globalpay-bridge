using GlobalPay.Domain.Modules.TimeImport;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface ITimesheetImportRepository
{
    Task AddAsync(TimesheetImport import, CancellationToken cancellationToken = default);

    Task<TimesheetImport?> GetByIdAsync(Guid importId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByChecksumAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        string sha256,
        CancellationToken cancellationToken = default);
}
