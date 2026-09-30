using GlobalPay.Domain.Common;

namespace GlobalPay.UnitTests;

public sealed class EntityTests
{
    [Fact]
    public void Equality_IsBasedOnIdAndType()
    {
        var id = Guid.NewGuid();

        var first = new SampleAggregate(id);
        var second = new SampleAggregate(id);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void Equality_DifferentIds_AreNotEqual()
    {
        Assert.NotEqual(new SampleAggregate(Guid.NewGuid()), new SampleAggregate(Guid.NewGuid()));
    }

    [Fact]
    public void DomainEvents_AreRaisedAndCleared()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.DoSomething();

        var domainEvent = Assert.Single(aggregate.DomainEvents);
        Assert.IsType<SampleEvent>(domainEvent);

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    private sealed record SampleEvent(Guid EventId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

    private sealed class SampleAggregate : AggregateRoot<Guid>
    {
        public SampleAggregate(Guid id)
            : base(id)
        {
        }

        public void DoSomething() =>
            RaiseDomainEvent(new SampleEvent(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}
