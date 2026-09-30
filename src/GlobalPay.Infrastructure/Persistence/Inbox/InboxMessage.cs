namespace GlobalPay.Infrastructure.Persistence.Inbox;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public InboxMessage(
        Guid id,
        string providerCode,
        string providerEventId,
        string payloadHash,
        DateTimeOffset receivedAtUtc)
    {
        Id = id;
        ProviderCode = providerCode;
        ProviderEventId = providerEventId;
        PayloadHash = payloadHash;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid Id { get; private set; }

    public string ProviderCode { get; private set; } = null!;

    public string ProviderEventId { get; private set; } = null!;

    public string PayloadHash { get; private set; } = null!;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    public void MarkProcessed(DateTimeOffset processedAtUtc) => ProcessedAtUtc = processedAtUtc;
}
