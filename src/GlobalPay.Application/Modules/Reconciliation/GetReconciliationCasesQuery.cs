using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Pagination;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Common;
using GlobalPay.Contracts.Reconciliation;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Reconciliation;

namespace GlobalPay.Application.Modules.Reconciliation;

internal static class ReconciliationMapper
{
    public static ReconciliationCaseResponse ToResponse(ReconciliationCase reconciliationCase)
    {
        ArgumentNullException.ThrowIfNull(reconciliationCase);

        return new ReconciliationCaseResponse(
            reconciliationCase.Id,
            reconciliationCase.PaymentBatchId,
            reconciliationCase.PaymentInstructionId,
            reconciliationCase.Reason.ToString(),
            reconciliationCase.Detail,
            reconciliationCase.Status.ToString(),
            reconciliationCase.Owner,
            reconciliationCase.ResolutionNote,
            reconciliationCase.OpenedAtUtc,
            reconciliationCase.ResolvedAtUtc);
    }
}

public sealed record GetReconciliationCasesQuery(
    string? Status,
    string? Reason,
    Guid? PaymentBatchId,
    string? Cursor,
    int Limit) : IQuery<PagedResponse<ReconciliationCaseResponse>>;

internal sealed class GetReconciliationCasesQueryHandler
    : IQueryHandler<GetReconciliationCasesQuery, PagedResponse<ReconciliationCaseResponse>>
{
    private const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    private readonly IReconciliationCaseRepository _reconciliationCases;
    private readonly ITenantContext _tenantContext;
    private readonly IAccessPolicy _accessPolicy;

    public GetReconciliationCasesQueryHandler(
        IReconciliationCaseRepository reconciliationCases,
        ITenantContext tenantContext,
        IAccessPolicy accessPolicy)
    {
        _reconciliationCases = reconciliationCases;
        _tenantContext = tenantContext;
        _accessPolicy = accessPolicy;
    }

    public async Task<Result<PagedResponse<ReconciliationCaseResponse>>> HandleAsync(
        GetReconciliationCasesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PagedResponse<ReconciliationCaseResponse>>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanPay && !_accessPolicy.CanAudit)
        {
            return Result.Failure<PagedResponse<ReconciliationCaseResponse>>(
                Error.Forbidden("auth.forbidden", "You are not allowed to read reconciliation exceptions."));
        }

        var limit = query.Limit <= 0 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);

        var filter = new ReconciliationCaseFilter(query.Status, query.Reason, query.PaymentBatchId);

        var rows = await _reconciliationCases
            .SearchAsync(filter, query.Cursor, limit + 1, cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > limit;
        var items = hasMore ? rows.Take(limit).ToArray() : rows.ToArray();

        string? nextCursor = null;

        if (hasMore && items.Length > 0)
        {
            var last = items[^1];
            nextCursor = CursorCodec.Encode(
                last.OpenedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                last.Id);
        }

        var response = items.Select(ReconciliationMapper.ToResponse).ToArray();

        return new PagedResponse<ReconciliationCaseResponse>(response, nextCursor, response.Length);
    }
}
