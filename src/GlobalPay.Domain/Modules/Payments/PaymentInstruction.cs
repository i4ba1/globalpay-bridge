using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Payments;

public sealed class PaymentInstruction : Entity<Guid>, ITenantScoped
{
    private PaymentInstruction()
    {
    }

    private PaymentInstruction(
        Guid id,
        Guid tenantId,
        Guid batchId,
        Guid workerPaymentId,
        Guid workerId,
        decimal amount,
        Currency currency)
        : base(id)
    {
        TenantId = tenantId;
        BatchId = batchId;
        WorkerPaymentId = workerPaymentId;
        WorkerId = workerId;
        Amount = amount;
        Currency = currency;
        Status = PaymentInstructionStatus.Pending;
    }

    public Guid TenantId { get; private set; }

    public Guid BatchId { get; private set; }

    public Guid WorkerPaymentId { get; private set; }

    public Guid WorkerId { get; private set; }

    public decimal Amount { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public PaymentInstructionStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    public DateTimeOffset? LastEventAtUtc { get; private set; }

    internal static PaymentInstruction Create(
        Guid tenantId,
        Guid batchId,
        Guid workerPaymentId,
        Guid workerId,
        decimal amount,
        Currency currency)
    {
        Guard.NotNull(currency, nameof(currency));

        if (amount < 0m)
        {
            throw new DomainException(
                "Payment instruction amount must not be negative.",
                "payments.instruction_negative_amount");
        }

        return new PaymentInstruction(
            Guid.NewGuid(),
            tenantId,
            batchId,
            Guard.NotEmpty(workerPaymentId, nameof(workerPaymentId)),
            Guard.NotEmpty(workerId, nameof(workerId)),
            amount,
            currency);
    }

    internal void MarkSubmitted(string? providerReference, DateTimeOffset atUtc)
    {
        if (Status != PaymentInstructionStatus.Pending)
        {
            return;
        }

        Status = PaymentInstructionStatus.Submitted;
        ProviderReference = providerReference;
        LastEventAtUtc = atUtc;
    }

    internal bool ApplyProviderEvent(ProviderEventType eventType, string? providerReference, string? failureCode, DateTimeOffset occurredAtUtc)
    {
        var next = eventType switch
        {
            ProviderEventType.Accepted => PaymentInstructionStatus.Accepted,
            ProviderEventType.Settled => PaymentInstructionStatus.Settled,
            ProviderEventType.Failed => PaymentInstructionStatus.Failed,
            _ => Status,
        };

        if (!IsLegalTransition(Status, next))
        {
            return false;
        }

        Status = next;
        ProviderReference = providerReference ?? ProviderReference;
        FailureCode = eventType == ProviderEventType.Failed ? failureCode ?? "provider.failed" : FailureCode;
        LastEventAtUtc = occurredAtUtc;

        return true;
    }

    private static bool IsLegalTransition(PaymentInstructionStatus current, PaymentInstructionStatus next)
    {
        if (current is PaymentInstructionStatus.Settled or PaymentInstructionStatus.Failed)
        {
            return false;
        }

        return next switch
        {
            PaymentInstructionStatus.Accepted => current == PaymentInstructionStatus.Submitted,
            PaymentInstructionStatus.Settled => current is PaymentInstructionStatus.Submitted or PaymentInstructionStatus.Accepted,
            PaymentInstructionStatus.Failed => current is PaymentInstructionStatus.Pending
                or PaymentInstructionStatus.Submitted
                or PaymentInstructionStatus.Accepted,
            _ => false,
        };
    }
}
