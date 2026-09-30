using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Audit;

public sealed class AuditEvent : Entity<long>, ITenantScoped
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid tenantId,
        string actor,
        string action,
        string targetType,
        Guid targetId,
        string? traceId,
        string? data,
        DateTimeOffset occurredAtUtc)
    {
        TenantId = tenantId;
        Actor = actor;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        TraceId = traceId;
        Data = data;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid TenantId { get; private set; }

    public string Actor { get; private set; } = null!;

    public string Action { get; private set; } = null!;

    public string TargetType { get; private set; } = null!;

    public Guid TargetId { get; private set; }

    public string? TraceId { get; private set; }

    public string? Data { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static AuditEvent Record(
        Guid tenantId,
        string actor,
        string action,
        string targetType,
        Guid targetId,
        DateTimeOffset occurredAtUtc,
        string? traceId = null,
        string? data = null)
    {
        Guard.NotEmpty(tenantId, nameof(tenantId));
        Guard.NotNullOrWhiteSpace(actor, nameof(actor));
        Guard.NotNullOrWhiteSpace(action, nameof(action));
        Guard.NotNullOrWhiteSpace(targetType, nameof(targetType));
        Guard.NotEmpty(targetId, nameof(targetId));

        return new AuditEvent(tenantId, actor, action, targetType, targetId, traceId, data, occurredAtUtc);
    }
}
