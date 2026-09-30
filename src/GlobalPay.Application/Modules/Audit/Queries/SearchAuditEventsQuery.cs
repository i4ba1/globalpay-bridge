using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Pagination;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Audit;
using GlobalPay.Contracts.Common;
using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Modules.Audit.Queries;

public sealed record SearchAuditEventsQuery(
    string? Actor,
    string? Action,
    string? TargetType,
    Guid? TargetId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Cursor,
    int Limit) : IQuery<PagedResponse<AuditEventResponse>>;

internal sealed class SearchAuditEventsQueryHandler
    : IQueryHandler<SearchAuditEventsQuery, PagedResponse<AuditEventResponse>>
{
    private const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    private readonly IAuditEventRepository _auditEvents;
    private readonly ITenantContext _tenantContext;
    private readonly IAccessPolicy _accessPolicy;

    public SearchAuditEventsQueryHandler(
        IAuditEventRepository auditEvents,
        ITenantContext tenantContext,
        IAccessPolicy accessPolicy)
    {
        _auditEvents = auditEvents;
        _tenantContext = tenantContext;
        _accessPolicy = accessPolicy;
    }

    public async Task<Result<PagedResponse<AuditEventResponse>>> HandleAsync(
        SearchAuditEventsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PagedResponse<AuditEventResponse>>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanAudit)
        {
            return Result.Failure<PagedResponse<AuditEventResponse>>(
                Error.Forbidden("auth.forbidden", "You are not allowed to read the audit timeline."));
        }

        var limit = query.Limit <= 0 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);

        var filter = new AuditEventFilter(
            query.Actor,
            query.Action,
            query.TargetType,
            query.TargetId,
            query.FromUtc,
            query.ToUtc);

        var rows = await _auditEvents
            .SearchAsync(filter, query.Cursor, limit + 1, cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > limit;
        var items = hasMore ? rows.Take(limit).ToArray() : rows.ToArray();

        string? nextCursor = null;

        if (hasMore && items.Length > 0)
        {
            var last = items[^1];
            nextCursor = CursorCodec.Encode(
                last.OccurredAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                last.Id);
        }

        var response = items
            .Select(auditEvent => new AuditEventResponse(
                auditEvent.Id,
                auditEvent.TenantId,
                auditEvent.Actor,
                auditEvent.Action,
                auditEvent.TargetType,
                auditEvent.TargetId,
                auditEvent.TraceId,
                auditEvent.OccurredAtUtc))
            .ToArray();

        return new PagedResponse<AuditEventResponse>(response, nextCursor, response.Length);
    }
}
