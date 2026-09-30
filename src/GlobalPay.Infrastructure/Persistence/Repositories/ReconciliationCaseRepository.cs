using GlobalPay.Application.Abstractions.Pagination;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Reconciliation;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class ReconciliationCaseRepository : IReconciliationCaseRepository
{
    private readonly GlobalPayDbContext _context;

    public ReconciliationCaseRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(
        ReconciliationCase reconciliationCase,
        CancellationToken cancellationToken = default) =>
        await _context.ReconciliationCases.AddAsync(reconciliationCase, cancellationToken).ConfigureAwait(false);

    public Task<ReconciliationCase?> GetByIdAsync(Guid caseId, CancellationToken cancellationToken = default) =>
        _context.ReconciliationCases.FirstOrDefaultAsync(
            reconciliationCase => reconciliationCase.Id == caseId,
            cancellationToken);

    public Task<bool> ExistsForInstructionAsync(
        Guid paymentInstructionId,
        CancellationToken cancellationToken = default) =>
        _context.ReconciliationCases.AnyAsync(
            reconciliationCase => reconciliationCase.PaymentInstructionId == paymentInstructionId
                                  && (reconciliationCase.Status == ReconciliationCaseStatus.Open
                                      || reconciliationCase.Status == ReconciliationCaseStatus.Investigating),
            cancellationToken);

    public async Task<IReadOnlyList<ReconciliationCase>> SearchAsync(
        ReconciliationCaseFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.ReconciliationCases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Status)
            && Enum.TryParse<ReconciliationCaseStatus>(filter.Status, ignoreCase: true, out var status))
        {
            query = query.Where(reconciliationCase => reconciliationCase.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Reason)
            && Enum.TryParse<ReconciliationReason>(filter.Reason, ignoreCase: true, out var reason))
        {
            query = query.Where(reconciliationCase => reconciliationCase.Reason == reason);
        }

        if (filter.PaymentBatchId is { } batchId)
        {
            query = query.Where(reconciliationCase => reconciliationCase.PaymentBatchId == batchId);
        }

        if (CursorCodec.TryDecode(afterCursor, out var parts)
            && parts.Count == 2
            && DateTimeOffset.TryParse(
                parts[0],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var openedAtUtc)
            && Guid.TryParse(parts[1], out var lastId))
        {
            query = query.Where(reconciliationCase =>
                reconciliationCase.OpenedAtUtc < openedAtUtc
                || (reconciliationCase.OpenedAtUtc == openedAtUtc && reconciliationCase.Id.CompareTo(lastId) < 0));
        }

        return await query
            .OrderByDescending(reconciliationCase => reconciliationCase.OpenedAtUtc)
            .ThenByDescending(reconciliationCase => reconciliationCase.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
