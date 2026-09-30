using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed record PayrollComponentInput(
    string Code,
    CompensationType Type,
    CalculationRule Rule,
    decimal Amount);

public sealed record PayrollWorkerInput(
    Guid WorkerId,
    Guid ContractId,
    Currency Currency,
    decimal Units,
    IReadOnlyList<PayrollComponentInput> Components);

public sealed record PayrollLineResult(
    string ComponentCode,
    CompensationType Type,
    CalculationRule Rule,
    decimal Amount);

public sealed record PayrollWorkerResult(
    Guid WorkerId,
    Guid ContractId,
    Currency Currency,
    decimal Gross,
    decimal Deduction,
    decimal Net,
    IReadOnlyList<PayrollLineResult> Lines);

public static class PayrollCalculator
{
    public const string RuleVersion = "demo-rules-v1";

    public static PayrollWorkerResult Calculate(PayrollWorkerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Guard.NotNull(input.Currency, nameof(input));
        ArgumentNullException.ThrowIfNull(input.Components);

        if (input.Units < 0m)
        {
            throw new DomainException("Units must not be negative.", "payroll.units_negative");
        }

        var currency = input.Currency;
        var lines = new List<PayrollLineResult>();
        var gross = Money.Zero(currency);

        foreach (var component in GrossComponents(input.Components))
        {
            var amount = component.Rule switch
            {
                CalculationRule.HourlyRate => Money.Of(component.Amount, currency).Multiply(input.Units),
                CalculationRule.FixedAmount => Money.Of(component.Amount, currency),
                _ => throw new DomainException(
                    $"Rule '{component.Rule}' cannot produce a gross amount.",
                    "payroll.rule_invalid"),
            };

            lines.Add(new PayrollLineResult(component.Code, component.Type, component.Rule, amount.Amount));
            gross = gross.Add(amount);
        }

        var deduction = Money.Zero(currency);

        foreach (var component in DeductionComponents(input.Components))
        {
            var amount = component.Rule switch
            {
                CalculationRule.FixedAmount => Money.Of(component.Amount, currency),
                CalculationRule.PercentOfGross => gross.Multiply(component.Amount / 100m),
                _ => throw new DomainException(
                    $"Rule '{component.Rule}' cannot produce a deduction.",
                    "payroll.rule_invalid"),
            };

            lines.Add(new PayrollLineResult(component.Code, CompensationType.Deduction, component.Rule, amount.Amount));
            deduction = deduction.Add(amount);
        }

        var net = gross.Subtract(deduction);

        if (net.IsNegative)
        {
            net = Money.Zero(currency);
        }

        return new PayrollWorkerResult(
            input.WorkerId,
            input.ContractId,
            currency,
            gross.Amount,
            deduction.Amount,
            net.Amount,
            lines);
    }

    private static IEnumerable<PayrollComponentInput> GrossComponents(IReadOnlyList<PayrollComponentInput> components) =>
        components
            .Where(component => component.Type != CompensationType.Deduction)
            .OrderBy(component => component.Code, StringComparer.Ordinal);

    private static IEnumerable<PayrollComponentInput> DeductionComponents(IReadOnlyList<PayrollComponentInput> components) =>
        components
            .Where(component => component.Type == CompensationType.Deduction)
            .OrderBy(component => component.Code, StringComparer.Ordinal);
}
