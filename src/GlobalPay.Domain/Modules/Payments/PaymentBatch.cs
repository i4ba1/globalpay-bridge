using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Payments;

public sealed record PaymentInstructionSpec(Guid WorkerPaymentId, Guid WorkerId, decimal Amount);

public sealed class PaymentBatch : AggregateRoot<Guid>, ITenantScoped
{
    private readonly List<PaymentInstruction> _instructions = [];

    private PaymentBatch()
    {
    }

    private PaymentBatch(
        Guid id,
        Guid tenantId,
        Guid payrollRunId,
        Currency currency,
        string idempotencyKey,
        string createdBy,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        PayrollRunId = payrollRunId;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        Status = PaymentBatchStatus.Created;
    }

    public Guid TenantId { get; private set; }

    public Guid PayrollRunId { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public string IdempotencyKey { get; private set; } = null!;

    public string CreatedBy { get; private set; } = null!;

    public PaymentBatchStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? SettledAtUtc { get; private set; }

    public IReadOnlyCollection<PaymentInstruction> Instructions => _instructions.AsReadOnly();

    public decimal TotalAmount => _instructions.Sum(instruction => instruction.Amount);

    public static PaymentBatch Create(
        Guid tenantId,
        Guid payrollRunId,
        Currency currency,
        string idempotencyKey,
        string createdBy,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<PaymentInstructionSpec> instructions)
    {
        Guard.NotNull(currency, nameof(currency));
        Guard.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        Guard.NotNullOrWhiteSpace(createdBy, nameof(createdBy));
        ArgumentNullException.ThrowIfNull(instructions);

        if (instructions.Count == 0)
        {
            throw new DomainException(
                "A payment batch requires at least one instruction.",
                "payments.instructions_required");
        }

        var batch = new PaymentBatch(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(payrollRunId, nameof(payrollRunId)),
            currency,
            idempotencyKey,
            createdBy,
            createdAtUtc);

        foreach (var spec in instructions)
        {
            batch._instructions.Add(PaymentInstruction.Create(
                tenantId,
                batch.Id,
                spec.WorkerPaymentId,
                spec.WorkerId,
                spec.Amount,
                currency));
        }

        return batch;
    }

    public void MarkSubmitted(string? providerReference, DateTimeOffset atUtc)
    {
        if (Status != PaymentBatchStatus.Created)
        {
            throw new DomainException(
                $"Only a created batch can be submitted; current status is '{Status}'.",
                "payments.submit_not_allowed");
        }

        foreach (var instruction in _instructions)
        {
            instruction.MarkSubmitted(providerReference, atUtc);
        }

        Status = PaymentBatchStatus.Submitted;
        ProviderReference = providerReference;
        SubmittedAtUtc = atUtc;
    }

    public bool ApplyProviderEvent(
        Guid instructionId,
        ProviderEventType eventType,
        string? providerReference,
        string? failureCode,
        DateTimeOffset occurredAtUtc)
    {
        var instruction = _instructions.FirstOrDefault(candidate => candidate.Id == instructionId);

        if (instruction is null)
        {
            return false;
        }

        var applied = instruction.ApplyProviderEvent(eventType, providerReference, failureCode, occurredAtUtc);

        if (applied)
        {
            RecomputeStatus(occurredAtUtc);
        }

        return applied;
    }

    public void RecomputeStatus(DateTimeOffset atUtc)
    {
        if (_instructions.All(instruction => instruction.Status == PaymentInstructionStatus.Settled))
        {
            Status = PaymentBatchStatus.Settled;
            SettledAtUtc ??= atUtc;

            return;
        }

        if (_instructions.Any(instruction => instruction.Status == PaymentInstructionStatus.Settled))
        {
            Status = PaymentBatchStatus.PartiallySettled;

            return;
        }

        if (_instructions.All(instruction => instruction.Status == PaymentInstructionStatus.Failed))
        {
            Status = PaymentBatchStatus.Failed;

            return;
        }

        if (_instructions.Any(instruction => instruction.Status == PaymentInstructionStatus.Failed))
        {
            Status = PaymentBatchStatus.Failed;

            return;
        }

        Status = PaymentBatchStatus.Submitted;
    }
}
