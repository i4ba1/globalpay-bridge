namespace GlobalPay.Application.Abstractions.Messaging;

public sealed record OutboxEnvelope(Guid Id, string Type, string Payload, int Attempts);

public interface IOutboxWriter
{
    Task EnqueueAsync(string type, string payload, CancellationToken cancellationToken = default);
}

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxEnvelope>> LeaseAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan nextAttemptDelay,
        CancellationToken cancellationToken = default);
}

public interface IInboxStore
{
    Task<bool> TryAddAsync(
        string providerCode,
        string providerEventId,
        string payloadHash,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        string providerCode,
        string providerEventId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default);
}
