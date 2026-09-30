namespace GlobalPay.Infrastructure.Persistence.Idempotency;

public sealed class IdempotencyRecordEntry
{
    private IdempotencyRecordEntry()
    {
    }

    public IdempotencyRecordEntry(
        Guid id,
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        string requestFingerprint,
        int outcome,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        OperationCode = operationCode;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint;
        Outcome = outcome;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string OperationCode { get; private set; } = null!;

    public string IdempotencyKey { get; private set; } = null!;

    public string RequestFingerprint { get; private set; } = null!;

    public int Outcome { get; private set; }

    public int? StatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }
}
