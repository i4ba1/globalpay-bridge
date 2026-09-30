using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Workforce;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class ContractRepository : IContractRepository
{
    private readonly GlobalPayDbContext _context;

    public ContractRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(Contract contract, CancellationToken cancellationToken = default) =>
        await _context.Contracts.AddAsync(contract, cancellationToken).ConfigureAwait(false);

    public Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        _context.Contracts
            .Include(contract => contract.Components)
            .FirstOrDefaultAsync(contract => contract.Id == contractId, cancellationToken);

    public Task<Contract?> GetEffectiveContractAsync(
        Guid workerId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default) =>
        _context.Contracts
            .Include(contract => contract.Components)
            .Where(contract => contract.WorkerId == workerId
                               && contract.StartDate <= effectiveOn
                               && (contract.EndDate == null || contract.EndDate >= effectiveOn))
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOverlappingContractAsync(
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var effectiveEnd = endDate ?? DateOnly.MaxValue;

        return _context.Contracts.AnyAsync(
            contract => contract.WorkerId == workerId
                        && contract.StartDate <= effectiveEnd
                        && (contract.EndDate == null || contract.EndDate >= startDate),
            cancellationToken);
    }
}
