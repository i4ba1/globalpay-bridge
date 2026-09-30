using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.UnitTests;

public sealed class ContractTests
{
    private static readonly Currency Gbp = Currency.FromCode("GBP");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithEndBeforeStart_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => NewContract(
            new DateOnly(2026, 9, 30),
            new DateOnly(2026, 9, 1)));

        Assert.Equal("workforce.contract_dates_invalid", exception.Code);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-06-30", "2026-06-30", "2026-12-31", true)]
    [InlineData("2026-01-01", "2026-06-30", "2026-07-01", "2026-12-31", false)]
    [InlineData("2026-01-01", null, "2030-01-01", null, true)]
    [InlineData("2026-01-01", "2026-03-31", "2026-04-01", null, false)]
    public void RangesOverlap_DetectsConflicts(string firstStart, string? firstEnd, string secondStart, string? secondEnd, bool expected)
    {
        var overlaps = Contract.RangesOverlap(
            DateOnly.Parse(firstStart, System.Globalization.CultureInfo.InvariantCulture),
            firstEnd is null ? null : DateOnly.Parse(firstEnd, System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.Parse(secondStart, System.Globalization.CultureInfo.InvariantCulture),
            secondEnd is null ? null : DateOnly.Parse(secondEnd, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, overlaps);
    }

    [Fact]
    public void IsEffectiveOn_RespectsBounds()
    {
        var contract = NewContract(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        Assert.True(contract.IsEffectiveOn(new DateOnly(2026, 1, 1)));
        Assert.True(contract.IsEffectiveOn(new DateOnly(2026, 6, 30)));
        Assert.False(contract.IsEffectiveOn(new DateOnly(2025, 12, 31)));
        Assert.False(contract.IsEffectiveOn(new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void AddComponent_DuplicateCode_IsRejected()
    {
        var contract = NewContract(null, null);
        contract.AddComponent(CompensationType.Earning, "BASE", CalculationRule.HourlyRate, 20m);

        var exception = Assert.Throws<DomainException>(() =>
            contract.AddComponent(CompensationType.Earning, "base", CalculationRule.FixedAmount, 10m));

        Assert.Equal("workforce.component_duplicate", exception.Code);
    }

    [Fact]
    public void AddComponent_InvalidRuleForType_IsRejected()
    {
        var contract = NewContract(null, null);

        var exception = Assert.Throws<DomainException>(() =>
            contract.AddComponent(CompensationType.Allowance, "TRANSPORT", CalculationRule.HourlyRate, 10m));

        Assert.Equal("workforce.component_rule_invalid", exception.Code);
    }

    [Fact]
    public void AddComponent_PercentAbove100_IsRejected()
    {
        var contract = NewContract(null, null);

        var exception = Assert.Throws<DomainException>(() =>
            contract.AddComponent(CompensationType.Deduction, "TAX", CalculationRule.PercentOfGross, 150m));

        Assert.Equal("workforce.component_percent_out_of_range", exception.Code);
    }

    private static Contract NewContract(DateOnly? start, DateOnly? end) =>
        Contract.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start ?? new DateOnly(2026, 1, 1),
            end,
            Gbp,
            Now);
}
