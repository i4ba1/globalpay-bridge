using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.TimeImport;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class TimesheetImportRepository : ITimesheetImportRepository
{
    private readonly GlobalPayDbContext _context;

    public TimesheetImportRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(TimesheetImport import, CancellationToken cancellationToken = default) =>
        await _context.TimesheetImports.AddAsync(import, cancellationToken).ConfigureAwait(false);

    public Task<TimesheetImport?> GetByIdAsync(Guid importId, CancellationToken cancellationToken = default) =>
        _context.TimesheetImports.FirstOrDefaultAsync(import => import.Id == importId, cancellationToken);

    public Task<bool> ExistsByChecksumAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        string sha256,
        CancellationToken cancellationToken = default) =>
        _context.TimesheetImports.AnyAsync(
            import => import.PeriodStart == periodStart
                      && import.PeriodEnd == periodEnd
                      && import.Sha256 == sha256,
            cancellationToken);
}

internal sealed class TimeEntryRepository : ITimeEntryRepository
{
    private readonly GlobalPayDbContext _context;

    public TimeEntryRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddRangeAsync(
        IReadOnlyCollection<TimeEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        await _context.TimeEntries.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetForImportAsync(
        Guid importId,
        CancellationToken cancellationToken = default) =>
        await _context.TimeEntries
            .Where(entry => entry.ImportId == importId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> GetWorkerIdsForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importIds);

        if (importIds.Count == 0)
        {
            return [];
        }

        var ids = importIds.Distinct().ToArray();

        return await _context.TimeEntries
            .Where(entry => ids.Contains(entry.ImportId))
            .Select(entry => entry.WorkerId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> SumUnitsByWorkerForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(importIds);

        if (importIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var ids = importIds.Distinct().ToArray();

        var rows = await _context.TimeEntries
            .Where(entry => ids.Contains(entry.ImportId))
            .GroupBy(entry => entry.WorkerId)
            .Select(group => new { WorkerId = group.Key, Units = group.Sum(entry => entry.Units) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.WorkerId, row => row.Units);
    }
}
