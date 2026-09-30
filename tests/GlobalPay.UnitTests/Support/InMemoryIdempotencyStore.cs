using GlobalPay.Application.Abstractions.Idempotency;

namespace GlobalPay.UnitTests.Support;

internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _records = new(StringComparer.Ordinal);

    public Task<IdempotencyRecord?> FindAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_records.TryGetValue(Key(tenantId, operationCode, idempotencyKey), out var record) ? record : null);

    public Task<IdempotencyBeginResult> BeginAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var key = Key(tenantId, operationCode, idempotencyKey);

        if (_records.TryGetValue(key, out var existing))
        {
            return Task.FromResult(new IdempotencyBeginResult(existing, false));
        }

        var record = new IdempotencyRecord(
            Guid.NewGuid(),
            tenantId,
            operationCode,
            idempotencyKey,
            requestFingerprint,
            IdempotencyOutcome.InProgress,
            null,
            null,
            DateTimeOffset.UtcNow,
            null);

        _records[key] = record;

        return Task.FromResult(new IdempotencyBeginResult(record, true));
    }

    public Task CompleteAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        var pair = _records.FirstOrDefault(entry => entry.Value.Id == recordId);

        if (pair.Key is not null)
        {
            _records[pair.Key] = pair.Value with
            {
                Outcome = IdempotencyOutcome.Completed,
                StatusCode = statusCode,
                ResponseBody = responseBody,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        return Task.CompletedTask;
    }

    public Task AbandonAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var pair = _records.FirstOrDefault(entry => entry.Value.Id == recordId);

        if (pair.Key is not null)
        {
            _records.Remove(pair.Key);
        }

        return Task.CompletedTask;
    }

    private static string Key(Guid tenantId, string operationCode, string idempotencyKey) =>
        $"{tenantId:D}:{operationCode}:{idempotencyKey}";
}
