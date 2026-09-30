using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.UnitTests;

public sealed class MoneyTests
{
    private static readonly Currency Usd = Currency.FromCode("USD");
    private static readonly Currency Eur = Currency.FromCode("EUR");

    [Fact]
    public void Of_RoundsToFourDecimalPlaces()
    {
        var money = Money.Of(1.234567m, Usd);

        Assert.Equal(1.2346m, money.Amount);
    }

    [Fact]
    public void Of_RoundsHalfToEven()
    {
        Assert.Equal(1.0000m, Money.Of(1.00005m, Usd).Amount);
        Assert.Equal(1.0002m, Money.Of(1.00015m, Usd).Amount);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var result = Money.Of(10.5000m, Usd).Add(Money.Of(2.2500m, Usd));

        Assert.Equal(12.7500m, result.Amount);
        Assert.Equal(Usd, result.Currency);
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var usd = Money.Of(10m, Usd);
        var eur = Money.Of(10m, Eur);

        var exception = Assert.Throws<DomainException>(() => usd.Add(eur));

        Assert.Equal("money.currency_mismatch", exception.Code);
    }

    [Fact]
    public void Operators_BehaveLikeMethods()
    {
        var sum = Money.Of(5m, Usd) + Money.Of(5m, Usd);
        var difference = Money.Of(5m, Usd) - Money.Of(2m, Usd);
        var product = Money.Of(5m, Usd) * 3m;

        Assert.Equal(10m, sum.Amount);
        Assert.Equal(3m, difference.Amount);
        Assert.Equal(15m, product.Amount);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(Money.Of(100m, Usd), Money.Of(100.0000m, Usd));
        Assert.NotEqual(Money.Of(100m, Usd), Money.Of(100m, Eur));
    }

    [Fact]
    public void Zero_IsZeroAndNotNegative()
    {
        var zero = Money.Zero(Usd);

        Assert.True(zero.IsZero);
        Assert.False(zero.IsNegative);
    }
}
