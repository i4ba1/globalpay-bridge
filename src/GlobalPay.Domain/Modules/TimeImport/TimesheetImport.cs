using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.TimeImport;

public sealed class TimesheetImport : AggregateRoot<Guid>, ITenantScoped
{
    private TimesheetImport()
    {
    }

    private TimesheetImport(
        Guid id,
        Guid tenantId,
        string blobName,
        string sha256,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        BlobName = blobName;
        Sha256 = sha256;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Status = ImportStatus.Registered;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TenantId { get; private set; }

    public string BlobName { get; private set; } = null!;

    public string Sha256 { get; private set; } = null!;

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public ImportStatus Status { get; private set; }

    public int TotalRows { get; private set; }

    public int ValidRows { get; private set; }

    public int InvalidRows { get; private set; }

    public string? ErrorReportBlobName { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static TimesheetImport Register(
        Guid tenantId,
        string blobName,
        string sha256,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateTimeOffset createdAtUtc,
        Guid? id = null)
    {
        if (periodEnd < periodStart)
        {
            throw new DomainException(
                "Import period end must not be before the period start.",
                "timeimport.period_invalid");
        }

        return new TimesheetImport(
            id ?? Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotNullOrWhiteSpace(blobName, nameof(blobName)),
            Guard.NotNullOrWhiteSpace(sha256, nameof(sha256)),
            periodStart,
            periodEnd,
            createdAtUtc);
    }

    public void MarkProcessing()
    {
        if (Status is not (ImportStatus.Registered or ImportStatus.Processing))
        {
            throw new DomainException(
                $"Import cannot start processing from status '{Status}'.",
                "timeimport.transition_invalid");
        }

        Status = ImportStatus.Processing;
    }

    public void Complete(int totalRows, int validRows, int invalidRows, string? errorReportBlobName, DateTimeOffset completedAtUtc)
    {
        TotalRows = totalRows;
        ValidRows = validRows;
        InvalidRows = invalidRows;
        ErrorReportBlobName = errorReportBlobName;
        Status = ImportStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Fail(string reason, DateTimeOffset completedAtUtc)
    {
        FailureReason = reason;
        Status = ImportStatus.Failed;
        CompletedAtUtc = completedAtUtc;
    }
}
