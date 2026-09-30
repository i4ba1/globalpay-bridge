namespace GlobalPay.Domain.Common;

public interface IAggregateRoot
{
    byte[] RowVersion { get; }
}
