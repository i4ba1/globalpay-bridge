using System.Collections.Frozen;
using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Finance;

public sealed class Currency : ValueObject
{
    private static readonly FrozenDictionary<string, Currency> SupportedCurrencies = BuildSupportedCurrencies();

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public static IReadOnlyCollection<Currency> All => SupportedCurrencies.Values;

    public static Currency FromCode(string code)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));

        return SupportedCurrencies.TryGetValue(code.Trim().ToUpperInvariant(), out var currency)
            ? currency
            : throw new DomainException($"Currency '{code}' is not supported.", "money.currency_unsupported");
    }

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && SupportedCurrencies.ContainsKey(code.Trim().ToUpperInvariant());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }

    public override string ToString() => Code;

    private static FrozenDictionary<string, Currency> BuildSupportedCurrencies()
    {
        var definitions = new (string Code, int MinorUnits)[]
        {
            ("USD", 2), ("EUR", 2), ("GBP", 2), ("JPY", 0), ("CHF", 2),
            ("AUD", 2), ("CAD", 2), ("NZD", 2), ("SGD", 2), ("HKD", 2),
            ("MYR", 2), ("PHP", 2), ("THB", 2), ("IDR", 2), ("VND", 0),
            ("INR", 2), ("AED", 2), ("ZAR", 2), ("NGN", 2), ("CNY", 2),
        };

        return definitions.ToFrozenDictionary(
            definition => definition.Code,
            definition => new Currency(definition.Code, definition.MinorUnits),
            StringComparer.Ordinal);
    }
}
