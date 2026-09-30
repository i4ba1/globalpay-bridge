using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed class WorkerPayment : Entity<Guid>, ITenantScoped
{
    private readonly List<WorkerPaymentLine> _lines = [];

    private WorkerPayment()
    {
    }

    private WorkerPayment(
        Guid id,
        Guid tenantId,
        Guid runVersionId,
        Guid workerId,
        Guid contractId,
        Currency currency,
        decimal gross,
        decimal deduction,
        decimal net)
        : base(id)
    {
        TenantId = tenantId;
        RunVersionId = runVersionId;
        WorkerId = workerId;
        ContractId = contractId;
        Currency = currency;
        Gross = gross;
        Deduction = deduction;
        Net = net;
    }

    public Guid TenantId { get; private set; }

    public Guid RunVersionId { get; private set; }

    public Guid WorkerId { get; private set; }

    public Guid ContractId { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public decimal Gross { get; private set; }

    public decimal Deduction { get; private set; }

    public decimal Net { get; private set; }

    public IReadOnlyCollection<WorkerPaymentLine> Lines => _lines.AsReadOnly();

    internal static WorkerPayment Create(
        Guid tenantId,
        Guid runVersionId,
        Guid workerId,
        Guid contractId,
        Currency currency,
        decimal gross,
        decimal deduction,
        decimal net,
        IReadOnlyList<PaymentLineSpec> lines)
    {
        Guard.NotNull(currency, nameof(currency));
        ArgumentNullException.ThrowIfNull(lines);

        if (gross < 0m || deduction < 0m || net < 0m)
        {
            throw new DomainException(
                "Payment amounts must not be negative.",
                "payroll.payment_negative");
        }

        var payment = new WorkerPayment(
            Guid.NewGuid(),
            tenantId,
            runVersionId,
            Guard.NotEmpty(workerId, nameof(workerId)),
            Guard.NotEmpty(contractId, nameof(contractId)),
            currency,
            gross,
            deduction,
            net);

        foreach (var line in lines)
        {
            payment._lines.Add(WorkerPaymentLine.Create(
                tenantId,
                payment.Id,
                line.ComponentCode,
                line.Type,
                line.Rule,
                line.Amount));
        }

        return payment;
    }
}
