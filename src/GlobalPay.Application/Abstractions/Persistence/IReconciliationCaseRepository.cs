using GlobalPay.Domain.Modules.Reconciliation;

namespace GlobalPay.Application.Abstractions.Persistence;

public sealed record ReconciliationCaseFilter(string? Status, string? Reason, Guid? PaymentBatchId);

public interface IReconciliationCaseRepository
{
    Task AddAsync(ReconciliationCase reconciliationCase, CancellationToken cancellationToken = default);

    Task<ReconciliationCase?> GetByIdAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReconciliationCase>> SearchAsync(
        ReconciliationCaseFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForInstructionAsync(Guid paymentInstructionId, CancellationToken cancellationToken = default);
}
