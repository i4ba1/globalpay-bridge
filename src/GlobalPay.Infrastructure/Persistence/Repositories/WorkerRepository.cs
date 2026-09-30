using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Workforce;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class WorkerRepository : IWorkerRepository
{
    private readonly GlobalPayDbContext _context;

    public WorkerRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(Worker worker, CancellationToken cancellationToken = default) =>
        await _context.Workers.AddAsync(worker, cancellationToken).ConfigureAwait(false);

    public Task<Worker?> GetByIdAsync(Guid workerId, CancellationToken cancellationToken = default) =>
        _context.Workers.FirstOrDefaultAsync(worker => worker.Id == workerId, cancellationToken);

    public Task<bool> ExternalRefExistsAsync(string externalRef, CancellationToken cancellationToken = default) =>
        _context.Workers.AnyAsync(worker => worker.ExternalRef == externalRef, cancellationToken);

    public async Task<IReadOnlyDictionary<string, Guid>> GetIdsByExternalRefsAsync(
        IReadOnlyCollection<string> externalRefs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalRefs);

        if (externalRefs.Count == 0)
        {
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }

        var refs = externalRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var rows = await _context.Workers
            .Where(worker => refs.Contains(worker.ExternalRef))
            .Select(worker => new { worker.ExternalRef, worker.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.ExternalRef, row => row.Id, StringComparer.OrdinalIgnoreCase);
    }
}
