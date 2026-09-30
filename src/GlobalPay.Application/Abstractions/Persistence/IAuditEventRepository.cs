using GlobalPay.Domain.Modules.Audit;

namespace GlobalPay.Application.Abstractions.Persistence;

public sealed record AuditEventFilter(
    string? Actor,
    string? Action,
    string? TargetType,
    Guid? TargetId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEvent>> SearchAsync(
        AuditEventFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default);
}
