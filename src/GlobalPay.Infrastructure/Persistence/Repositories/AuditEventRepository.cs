using System.Globalization;
using GlobalPay.Application.Abstractions.Pagination;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Audit;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly GlobalPayDbContext _context;

    public AuditEventRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) =>
        await _context.AuditEvents.AddAsync(auditEvent, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditEvent>> SearchAsync(
        AuditEventFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            query = query.Where(auditEvent => auditEvent.Actor == filter.Actor);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(auditEvent => auditEvent.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetType))
        {
            query = query.Where(auditEvent => auditEvent.TargetType == filter.TargetType);
        }

        if (filter.TargetId is { } targetId)
        {
            query = query.Where(auditEvent => auditEvent.TargetId == targetId);
        }

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAtUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(auditEvent => auditEvent.OccurredAtUtc <= toUtc);
        }

        if (TryDecodeCursor(afterCursor, out var occurredAtUtc, out var sequenceId))
        {
            query = query.Where(auditEvent =>
                auditEvent.OccurredAtUtc < occurredAtUtc
                || (auditEvent.OccurredAtUtc == occurredAtUtc && auditEvent.Id < sequenceId));
        }

        return await query
            .OrderByDescending(auditEvent => auditEvent.OccurredAtUtc)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryDecodeCursor(string? cursor, out DateTimeOffset occurredAtUtc, out long sequenceId)
    {
        occurredAtUtc = default;
        sequenceId = 0;

        if (!CursorCodec.TryDecode(cursor, out var parts) || parts.Count != 2)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
                   parts[0],
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out occurredAtUtc)
               && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out sequenceId);
    }
}
