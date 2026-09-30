using GlobalPay.Domain.Modules.TimeImport;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface ITimeEntryRepository
{
    Task AddRangeAsync(IReadOnlyCollection<TimeEntry> entries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntry>> GetForImportAsync(Guid importId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Workers that contributed time through the supplied imports. Calculation is scoped to a run's
    /// source imports so time from unrelated imports in the same period is never included.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetWorkerIdsForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal>> SumUnitsByWorkerForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default);
}
