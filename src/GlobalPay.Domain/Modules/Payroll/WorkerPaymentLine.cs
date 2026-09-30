using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed class WorkerPaymentLine : Entity<Guid>, ITenantScoped
{
    private WorkerPaymentLine()
    {
    }

    private WorkerPaymentLine(
        Guid id,
        Guid tenantId,
        Guid workerPaymentId,
        string componentCode,
        CompensationType type,
        CalculationRule rule,
        decimal amount)
        : base(id)
    {
        TenantId = tenantId;
        WorkerPaymentId = workerPaymentId;
        ComponentCode = componentCode;
        Type = type;
        Rule = rule;
        Amount = amount;
    }

    public Guid TenantId { get; private set; }

    public Guid WorkerPaymentId { get; private set; }

    public string ComponentCode { get; private set; } = null!;

    public CompensationType Type { get; private set; }

    public CalculationRule Rule { get; private set; }

    public decimal Amount { get; private set; }

    internal static WorkerPaymentLine Create(
        Guid tenantId,
        Guid workerPaymentId,
        string componentCode,
        CompensationType type,
        CalculationRule rule,
        decimal amount) =>
        new(
            Guid.NewGuid(),
            tenantId,
            workerPaymentId,
            Guard.NotNullOrWhiteSpace(componentCode, nameof(componentCode)),
            type,
            rule,
            amount);
}
