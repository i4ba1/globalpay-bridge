using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Reconciliation;

public sealed class ReconciliationCase : AggregateRoot<Guid>, ITenantScoped
{
    private ReconciliationCase()
    {
    }

    private ReconciliationCase(
        Guid id,
        Guid tenantId,
        Guid paymentBatchId,
        Guid paymentInstructionId,
        ReconciliationReason reason,
        string detail,
        DateTimeOffset openedAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        PaymentBatchId = paymentBatchId;
        PaymentInstructionId = paymentInstructionId;
        Reason = reason;
        Detail = detail;
        Status = ReconciliationCaseStatus.Open;
        OpenedAtUtc = openedAtUtc;
    }

    public Guid TenantId { get; private set; }

    public Guid PaymentBatchId { get; private set; }

    public Guid PaymentInstructionId { get; private set; }

    public ReconciliationReason Reason { get; private set; }

    public string Detail { get; private set; } = null!;

    public ReconciliationCaseStatus Status { get; private set; }

    public string? Owner { get; private set; }

    public string? ResolutionNote { get; private set; }

    public DateTimeOffset OpenedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public static ReconciliationCase Open(
        Guid tenantId,
        Guid paymentBatchId,
        Guid paymentInstructionId,
        ReconciliationReason reason,
        string detail,
        DateTimeOffset openedAtUtc)
    {
        Guard.NotNullOrWhiteSpace(detail, nameof(detail));

        return new ReconciliationCase(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(paymentBatchId, nameof(paymentBatchId)),
            Guard.NotEmpty(paymentInstructionId, nameof(paymentInstructionId)),
            reason,
            detail,
            openedAtUtc);
    }

    public void Assign(string owner)
    {
        Guard.NotNullOrWhiteSpace(owner, nameof(owner));

        Owner = owner;

        if (Status == ReconciliationCaseStatus.Open)
        {
            Status = ReconciliationCaseStatus.Investigating;
        }
    }

    public void Resolve(string note, DateTimeOffset resolvedAtUtc)
    {
        EnsureOpen();

        ResolutionNote = Guard.NotNullOrWhiteSpace(note, nameof(note));
        Status = ReconciliationCaseStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void WriteOff(string note, DateTimeOffset resolvedAtUtc)
    {
        EnsureOpen();

        ResolutionNote = Guard.NotNullOrWhiteSpace(note, nameof(note));
        Status = ReconciliationCaseStatus.WrittenOff;
        ResolvedAtUtc = resolvedAtUtc;
    }

    private void EnsureOpen()
    {
        if (Status is ReconciliationCaseStatus.Resolved or ReconciliationCaseStatus.WrittenOff)
        {
            throw new DomainException(
                $"A reconciliation case in status '{Status}' cannot be resolved again.",
                "reconciliation.case_closed");
        }
    }
}
