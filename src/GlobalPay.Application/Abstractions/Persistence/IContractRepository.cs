using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface IContractRepository
{
    Task AddAsync(Contract contract, CancellationToken cancellationToken = default);

    Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default);

    Task<Contract?> GetEffectiveContractAsync(
        Guid workerId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingContractAsync(
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);
}
