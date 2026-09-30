namespace GlobalPay.Domain.Common;

public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; }

    string? CreatedBy { get; }

    DateTimeOffset? ModifiedAtUtc { get; }

    string? ModifiedBy { get; }
}
