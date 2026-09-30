using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed class PayrollRun : AggregateRoot<Guid>, ITenantScoped
{
    private readonly List<Guid> _sourceImportIds = [];

    private PayrollRun()
    {
    }

    private PayrollRun(
        Guid id,
        Guid tenantId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Currency currency,
        string createdBy,
        string? note,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Currency = currency;
        CreatedBy = createdBy;
        Note = note;
        Status = PayrollRunStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TenantId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public PayrollRunStatus Status { get; private set; }

    public int CurrentVersion { get; private set; }

    public Guid? CurrentVersionId { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public string? Note { get; private set; }

    public string? DecidedBy { get; private set; }

    public string? DecisionReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public IReadOnlyCollection<Guid> SourceImportIds => _sourceImportIds.AsReadOnly();

    public static PayrollRun Create(
        Guid tenantId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Currency currency,
        IReadOnlyCollection<Guid> sourceImportIds,
        string createdBy,
        string? note,
        DateTimeOffset createdAtUtc)
    {
        Guard.NotNull(currency, nameof(currency));
        Guard.NotNullOrWhiteSpace(createdBy, nameof(createdBy));

        if (periodEnd < periodStart)
        {
            throw new DomainException(
                "Payroll period end must not be before the period start.",
                "payroll.period_invalid");
        }

        if (sourceImportIds is null || sourceImportIds.Count == 0)
        {
            throw new DomainException(
                "A payroll run requires at least one completed import.",
                "payroll.imports_required");
        }

        if (note is { Length: > 500 })
        {
            throw new DomainException("Note must not exceed 500 characters.", "payroll.note_too_long");
        }

        var run = new PayrollRun(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            periodStart,
            periodEnd,
            currency,
            createdBy,
            note,
            createdAtUtc);

        run._sourceImportIds.AddRange(sourceImportIds);

        return run;
    }

    public void AttachVersion(PayrollRunVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (Status is PayrollRunStatus.PendingApproval or PayrollRunStatus.Approved or PayrollRunStatus.Paid)
        {
            throw new DomainException(
                $"Payroll run cannot be recalculated while '{Status}'.",
                "payroll.recalculate_not_allowed");
        }

        if (version.PayrollRunId != Id)
        {
            throw new DomainException("Version does not belong to this payroll run.", "payroll.version_mismatch");
        }

        CurrentVersion = version.Version;
        CurrentVersionId = version.Id;
        Status = PayrollRunStatus.Calculated;
    }

    public void Submit(string actor, DateTimeOffset submittedAtUtc)
    {
        if (Status != PayrollRunStatus.Calculated)
        {
            throw new DomainException(
                $"Only a calculated run can be submitted; current status is '{Status}'.",
                "payroll.submit_not_allowed");
        }

        Guard.NotNullOrWhiteSpace(actor, nameof(actor));

        Status = PayrollRunStatus.PendingApproval;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void Approve(string actor, DateTimeOffset decidedAtUtc)
    {
        EnsureDecidable(actor);

        Status = PayrollRunStatus.Approved;
        DecidedBy = actor;
        DecisionReason = null;
        DecidedAtUtc = decidedAtUtc;
    }

    public void Reject(string actor, string reason, DateTimeOffset decidedAtUtc)
    {
        EnsureDecidable(actor);

        Status = PayrollRunStatus.Rejected;
        DecidedBy = actor;
        DecisionReason = Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        DecidedAtUtc = decidedAtUtc;
    }

    public void MarkPaid(DateTimeOffset paidAtUtc)
    {
        if (Status != PayrollRunStatus.Approved)
        {
            throw new DomainException(
                $"Only an approved run can be marked paid; current status is '{Status}'.",
                "payroll.mark_paid_not_allowed");
        }

        Status = PayrollRunStatus.Paid;
        DecidedAtUtc ??= paidAtUtc;
    }

    public bool IsCreatedBy(string userId) => string.Equals(CreatedBy, userId, StringComparison.Ordinal);

    public IReadOnlyCollection<string> AllowedActions(string userId, bool canManage, bool canApprove, bool canPay)
    {
        var actions = new List<string>();

        if (canManage && Status is PayrollRunStatus.Draft or PayrollRunStatus.Calculated or PayrollRunStatus.Rejected)
        {
            actions.Add(PayrollActions.Calculate);
        }

        if (canManage && Status == PayrollRunStatus.Calculated)
        {
            actions.Add(PayrollActions.Submit);
        }

        if (canApprove && Status == PayrollRunStatus.PendingApproval && !IsCreatedBy(userId))
        {
            actions.Add(PayrollActions.Approve);
            actions.Add(PayrollActions.Reject);
        }

        if (canPay && Status == PayrollRunStatus.Approved)
        {
            actions.Add(PayrollActions.CreatePaymentBatch);
        }

        if (Status == PayrollRunStatus.Approved)
        {
            actions.Add(PayrollActions.Export);
        }

        return actions;
    }

    private void EnsureDecidable(string actor)
    {
        if (Status != PayrollRunStatus.PendingApproval)
        {
            throw new DomainException(
                $"Only a run pending approval can be decided; current status is '{Status}'.",
                "payroll.decision_not_allowed");
        }

        Guard.NotNullOrWhiteSpace(actor, nameof(actor));

        if (IsCreatedBy(actor))
        {
            throw new DomainException(
                "The creator of a payroll run cannot approve or reject it.",
                "payroll.maker_checker_violation");
        }
    }
}
