namespace GlobalPay.Application.Abstractions;

public interface IAuditRecorder
{
    Task RecordAsync(
        string action,
        string targetType,
        Guid targetId,
        string? data = null,
        CancellationToken cancellationToken = default);
}
