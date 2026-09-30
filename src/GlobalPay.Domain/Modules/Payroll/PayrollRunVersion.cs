using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed class PayrollRunVersion : Entity<Guid>, ITenantScoped
{
    private readonly List<WorkerPayment> _payments = [];

    private PayrollRunVersion()
    {
    }

    private PayrollRunVersion(
        Guid id,
        Guid tenantId,
        Guid payrollRunId,
        int version,
        string ruleVersion,
        Currency currency,
        DateTimeOffset calculatedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        PayrollRunId = payrollRunId;
        Version = version;
        RuleVersion = ruleVersion;
        Currency = currency;
        CalculatedAtUtc = calculatedAtUtc;
    }

    public Guid TenantId { get; private set; }

    public Guid PayrollRunId { get; private set; }

    public int Version { get; private set; }

    public string RuleVersion { get; private set; } = null!;

    public Currency Currency { get; private set; } = null!;

    public decimal GrossTotal { get; private set; }

    public decimal DeductionTotal { get; private set; }

    public decimal NetTotal { get; private set; }

    public int WorkerCount { get; private set; }

    public bool IsImmutable { get; private set; }

    public DateTimeOffset CalculatedAtUtc { get; private set; }

    public IReadOnlyCollection<WorkerPayment> Payments => _payments.AsReadOnly();

    public static PayrollRunVersion Create(
        Guid tenantId,
        Guid payrollRunId,
        int version,
        string ruleVersion,
        Currency currency,
        DateTimeOffset calculatedAtUtc)
    {
        Guard.NotNull(currency, nameof(currency));

        return new PayrollRunVersion(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(payrollRunId, nameof(payrollRunId)),
            version,
            Guard.NotNullOrWhiteSpace(ruleVersion, nameof(ruleVersion)),
            currency,
            calculatedAtUtc);
    }

    public WorkerPayment AddPayment(
        Guid workerId,
        Guid contractId,
        decimal gross,
        decimal deduction,
        decimal net,
        IReadOnlyList<PaymentLineSpec> lines)
    {
        if (IsImmutable)
        {
            throw new DomainException(
                "A sealed payroll version cannot be modified.",
                "payroll.version_immutable");
        }

        if (_payments.Exists(payment => payment.WorkerId == workerId))
        {
            throw new DomainException(
                "A payroll version may contain only one result per worker.",
                "payroll.duplicate_worker_payment");
        }

        var payment = WorkerPayment.Create(TenantId, Id, workerId, contractId, Currency, gross, deduction, net, lines);
        _payments.Add(payment);

        return payment;
    }

    public void Seal()
    {
        GrossTotal = _payments.Sum(payment => payment.Gross);
        DeductionTotal = _payments.Sum(payment => payment.Deduction);
        NetTotal = _payments.Sum(payment => payment.Net);
        WorkerCount = _payments.Count;
        IsImmutable = true;
    }
}
