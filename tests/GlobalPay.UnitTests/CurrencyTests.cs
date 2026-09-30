using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.UnitTests;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData("usd", "USD")]
    [InlineData("  eur  ", "EUR")]
    [InlineData("Gbp", "GBP")]
    public void FromCode_NormalizesInput(string input, string expectedCode)
    {
        var currency = Currency.FromCode(input);

        Assert.Equal(expectedCode, currency.Code);
    }

    [Fact]
    public void FromCode_Unsupported_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Currency.FromCode("XYZ"));

        Assert.Equal("money.currency_unsupported", exception.Code);
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("xyz", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void IsSupported_ReturnsExpected(string? code, bool expected)
    {
        Assert.Equal(expected, Currency.IsSupported(code));
    }

    [Fact]
    public void All_ExposesSupportedCurrencies()
    {
        Assert.Contains(Currency.All, currency => currency.Code == "USD");
        Assert.Contains(Currency.All, currency => currency.Code == "JPY" && currency.MinorUnits == 0);
    }
}
