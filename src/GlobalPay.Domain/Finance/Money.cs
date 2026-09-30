using System.Globalization;
using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Finance;

public sealed class Money : ValueObject
{
    public const int DecimalScale = 4;

    private const MidpointRounding RoundingMode = MidpointRounding.ToEven;

    private Money(decimal amount, Currency currency)
    {
        Amount = decimal.Round(amount, DecimalScale, RoundingMode);
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public static Money Of(decimal amount, Currency currency)
    {
        Guard.NotNull(currency, nameof(currency));

        return new Money(amount, currency);
    }

    public static Money Of(decimal amount, string currencyCode) => new(amount, Currency.FromCode(currencyCode));

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money left, decimal factor) => left.Multiply(factor);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public Money Negate() => new(-Amount, Currency);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:F4} {Currency.Code}");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private void EnsureSameCurrency(Money other)
    {
        Guard.NotNull(other, nameof(other));

        if (Currency != other.Currency)
        {
            throw new DomainException(
                $"Cannot combine amounts in {Currency.Code} and {other.Currency.Code}.",
                "money.currency_mismatch");
        }
    }
}
