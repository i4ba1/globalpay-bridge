namespace GlobalPay.Domain.Common;

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new DomainException("Cannot access the value of a failed result.", "result.no_value");

    public static implicit operator Result<TValue>(TValue value) => new(value, true, Error.None);
}
