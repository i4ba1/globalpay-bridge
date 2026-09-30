using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Workforce;

public sealed class CompensationComponent : Entity<Guid>, ITenantScoped
{
    private CompensationComponent()
    {
    }

    private CompensationComponent(
        Guid id,
        Guid tenantId,
        Guid contractId,
        CompensationType type,
        string code,
        CalculationRule rule,
        decimal amount)
        : base(id)
    {
        TenantId = tenantId;
        ContractId = contractId;
        Type = type;
        Code = code;
        Rule = rule;
        Amount = amount;
    }

    public Guid TenantId { get; private set; }

    public Guid ContractId { get; private set; }

    public CompensationType Type { get; private set; }

    public string Code { get; private set; } = null!;

    public CalculationRule Rule { get; private set; }

    public decimal Amount { get; private set; }

    public static CompensationComponent Create(
        Guid tenantId,
        Guid contractId,
        CompensationType type,
        string code,
        CalculationRule rule,
        decimal amount)
    {
        Validate(type, rule, amount);

        return new CompensationComponent(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(contractId, nameof(contractId)),
            type,
            Guard.NotNullOrWhiteSpace(code, nameof(code)),
            rule,
            amount);
    }

    private static void Validate(CompensationType type, CalculationRule rule, decimal amount)
    {
        if (amount < 0m)
        {
            throw new DomainException(
                "Compensation amount must not be negative.",
                "workforce.component_negative_amount");
        }

        var ruleAllowed = type switch
        {
            CompensationType.Earning => rule is CalculationRule.HourlyRate or CalculationRule.FixedAmount,
            CompensationType.Allowance or CompensationType.Bonus => rule is CalculationRule.FixedAmount,
            CompensationType.Deduction => rule is CalculationRule.FixedAmount or CalculationRule.PercentOfGross,
            _ => false,
        };

        if (!ruleAllowed)
        {
            throw new DomainException(
                $"Rule '{rule}' is not valid for a '{type}' component.",
                "workforce.component_rule_invalid");
        }

        if (rule is CalculationRule.PercentOfGross && amount > 100m)
        {
            throw new DomainException(
                "Percentage deductions must be between 0 and 100.",
                "workforce.component_percent_out_of_range");
        }
    }
}
