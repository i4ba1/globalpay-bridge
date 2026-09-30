namespace GlobalPay.Application.Abstractions.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IdempotencyBeginResult> BeginAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default);

    Task AbandonAsync(Guid recordId, CancellationToken cancellationToken = default);
}
