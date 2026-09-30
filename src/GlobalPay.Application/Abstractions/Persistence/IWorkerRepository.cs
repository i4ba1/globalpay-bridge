using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface IWorkerRepository
{
    Task AddAsync(Worker worker, CancellationToken cancellationToken = default);

    Task<Worker?> GetByIdAsync(Guid workerId, CancellationToken cancellationToken = default);

    Task<bool> ExternalRefExistsAsync(string externalRef, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Guid>> GetIdsByExternalRefsAsync(
        IReadOnlyCollection<string> externalRefs,
        CancellationToken cancellationToken = default);
}
