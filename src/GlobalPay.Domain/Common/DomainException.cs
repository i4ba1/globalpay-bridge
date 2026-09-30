namespace GlobalPay.Domain.Common;

public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, string code)
        : base(message) => Code = code;

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? Code { get; }
}
