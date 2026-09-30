namespace GlobalPay.Contracts.Audit;

public sealed record AuditEventResponse(
    long SequenceId,
    Guid TenantId,
    string Actor,
    string Action,
    string TargetType,
    Guid TargetId,
    string? TraceId,
    DateTimeOffset OccurredAtUtc);
