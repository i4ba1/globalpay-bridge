using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Reconciliation;

namespace GlobalPay.UnitTests.Support;

internal sealed class InMemoryReconciliationCaseRepository : IReconciliationCaseRepository
{
    private readonly List<ReconciliationCase> _cases = [];

    public IReadOnlyList<ReconciliationCase> All => _cases;

    public Task AddAsync(ReconciliationCase reconciliationCase, CancellationToken cancellationToken = default)
    {
        _cases.Add(reconciliationCase);

        return Task.CompletedTask;
    }

    public Task<ReconciliationCase?> GetByIdAsync(Guid caseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cases.FirstOrDefault(reconciliationCase => reconciliationCase.Id == caseId));

    public Task<bool> ExistsForInstructionAsync(
        Guid paymentInstructionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_cases.Any(reconciliationCase =>
            reconciliationCase.PaymentInstructionId == paymentInstructionId
            && reconciliationCase.Status is ReconciliationCaseStatus.Open or ReconciliationCaseStatus.Investigating));

    public Task<IReadOnlyList<ReconciliationCase>> SearchAsync(
        ReconciliationCaseFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _cases.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(reconciliationCase =>
                string.Equals(reconciliationCase.Status.ToString(), filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            query = query.Where(reconciliationCase =>
                string.Equals(reconciliationCase.Reason.ToString(), filter.Reason, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.PaymentBatchId is { } batchId)
        {
            query = query.Where(reconciliationCase => reconciliationCase.PaymentBatchId == batchId);
        }

        return Task.FromResult<IReadOnlyList<ReconciliationCase>>(query
            .OrderByDescending(reconciliationCase => reconciliationCase.OpenedAtUtc)
            .ThenByDescending(reconciliationCase => reconciliationCase.Id)
            .Take(limit)
            .ToArray());
    }
}
