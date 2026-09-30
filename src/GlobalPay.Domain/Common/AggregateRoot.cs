namespace GlobalPay.Domain.Common;

public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public byte[] RowVersion { get; protected set; } = [];
}
