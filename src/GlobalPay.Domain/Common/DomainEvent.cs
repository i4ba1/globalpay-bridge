namespace GlobalPay.Domain.Common;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAtUtc)
        : this(Guid.NewGuid(), occurredAtUtc)
    {
    }

    protected DomainEvent(Guid eventId, DateTimeOffset occurredAtUtc)
    {
        EventId = eventId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
