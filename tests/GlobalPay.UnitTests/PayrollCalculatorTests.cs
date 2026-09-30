using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.UnitTests;

public sealed class PayrollCalculatorTests
{
    private static readonly Currency Usd = Currency.FromCode("USD");

    [Fact]
    public void HourlyEarning_MultipliesRateByUnits()
    {
        var input = new PayrollWorkerInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Usd,
            40m,
            [new PayrollComponentInput("BASE", CompensationType.Earning, CalculationRule.HourlyRate, 25m)]);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(1000m, result.Gross);
        Assert.Equal(0m, result.Deduction);
        Assert.Equal(1000m, result.Net);
    }

    [Fact]
    public void FixedComponents_SumIntoGrossAndDeductions()
    {
        var input = new PayrollWorkerInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Usd,
            160m,
            [
                new PayrollComponentInput("BASE", CompensationType.Earning, CalculationRule.HourlyRate, 10m),
                new PayrollComponentInput("TRANSPORT", CompensationType.Allowance, CalculationRule.FixedAmount, 50m),
                new PayrollComponentInput("BONUS", CompensationType.Bonus, CalculationRule.FixedAmount, 200m),
                new PayrollComponentInput("PENSION", CompensationType.Deduction, CalculationRule.FixedAmount, 100m),
            ]);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(1600m + 50m + 200m, result.Gross);
        Assert.Equal(100m, result.Deduction);
        Assert.Equal(1750m, result.Net);
    }

    [Fact]
    public void PercentDeduction_AppliesToGross()
    {
        var input = new PayrollWorkerInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Usd,
            100m,
            [
                new PayrollComponentInput("BASE", CompensationType.Earning, CalculationRule.HourlyRate, 20m),
                new PayrollComponentInput("TAX", CompensationType.Deduction, CalculationRule.PercentOfGross, 12.5m),
            ]);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(2000m, result.Gross);
        Assert.Equal(250m, result.Deduction);
        Assert.Equal(1750m, result.Net);
    }

    [Fact]
    public void Net_IsClampedAtZero_WhenDeductionsExceedGross()
    {
        var input = new PayrollWorkerInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Usd,
            1m,
            [
                new PayrollComponentInput("BASE", CompensationType.Earning, CalculationRule.FixedAmount, 100m),
                new PayrollComponentInput("ADVANCE", CompensationType.Deduction, CalculationRule.FixedAmount, 500m),
            ]);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(100m, result.Gross);
        Assert.Equal(500m, result.Deduction);
        Assert.Equal(0m, result.Net);
    }

    [Fact]
    public void Calculate_IsDeterministic_ForIdenticalInput()
    {
        var workerId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var first = PayrollCalculator.Calculate(BuildInput(workerId, contractId));
        var second = PayrollCalculator.Calculate(BuildInput(workerId, contractId));

        Assert.Equal(first.Gross, second.Gross);
        Assert.Equal(first.Deduction, second.Deduction);
        Assert.Equal(first.Net, second.Net);
        Assert.Equal(
            first.Lines.Select(line => (line.ComponentCode, line.Amount)),
            second.Lines.Select(line => (line.ComponentCode, line.Amount)));
    }

    [Fact]
    public void Lines_AreOrderedByComponentCode_ForStableSnapshots()
    {
        var input = new PayrollWorkerInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Usd,
            10m,
            [
                new PayrollComponentInput("ZULU", CompensationType.Allowance, CalculationRule.FixedAmount, 1m),
                new PayrollComponentInput("ALPHA", CompensationType.Earning, CalculationRule.FixedAmount, 1m),
                new PayrollComponentInput("MIKE", CompensationType.Bonus, CalculationRule.FixedAmount, 1m),
            ]);

        var result = PayrollCalculator.Calculate(input);

        Assert.Equal(["ALPHA", "MIKE", "ZULU"], result.Lines.Select(line => line.ComponentCode));
    }

    private static PayrollWorkerInput BuildInput(Guid workerId, Guid contractId) =>
        new(
            workerId,
            contractId,
            Usd,
            173.5m,
            [
                new PayrollComponentInput("BASE", CompensationType.Earning, CalculationRule.HourlyRate, 31.37m),
                new PayrollComponentInput("TRANSPORT", CompensationType.Allowance, CalculationRule.FixedAmount, 45m),
                new PayrollComponentInput("TAX", CompensationType.Deduction, CalculationRule.PercentOfGross, 18.75m),
                new PayrollComponentInput("PENSION", CompensationType.Deduction, CalculationRule.FixedAmount, 120m),
            ]);
}
