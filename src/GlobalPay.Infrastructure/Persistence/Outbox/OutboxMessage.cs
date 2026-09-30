namespace GlobalPay.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset availableAtUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
        AvailableAtUtc = availableAtUtc;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset AvailableAtUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public DateTimeOffset? LeaseUntilUtc { get; private set; }

    public string? Error { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static OutboxMessage Create(
        string type,
        string payload,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset availableAtUtc,
        Guid? id = null) =>
        new(id ?? Guid.NewGuid(), type, payload, occurredAtUtc, availableAtUtc);

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        LeaseUntilUtc = null;
        Error = null;
    }

    public void RecordAttempt(string? error, DateTimeOffset availableAtUtc)
    {
        Attempts++;
        Error = error;
        AvailableAtUtc = availableAtUtc;
        LeaseUntilUtc = null;
    }

    public void Lease(DateTimeOffset leaseUntilUtc) => LeaseUntilUtc = leaseUntilUtc;
}
