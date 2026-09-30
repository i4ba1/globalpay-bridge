using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.TimeImport;

public sealed class TimeEntry : Entity<Guid>, ITenantScoped
{
    private TimeEntry()
    {
    }

    private TimeEntry(
        Guid id,
        Guid tenantId,
        Guid importId,
        Guid workerId,
        DateOnly workDate,
        decimal units,
        string? componentCode)
        : base(id)
    {
        TenantId = tenantId;
        ImportId = importId;
        WorkerId = workerId;
        WorkDate = workDate;
        Units = units;
        ComponentCode = componentCode;
    }

    public Guid TenantId { get; private set; }

    public Guid ImportId { get; private set; }

    public Guid WorkerId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public decimal Units { get; private set; }

    public string? ComponentCode { get; private set; }

    public static TimeEntry Create(
        Guid tenantId,
        Guid importId,
        Guid workerId,
        DateOnly workDate,
        decimal units,
        string? componentCode = null)
    {
        if (units < 0m)
        {
            throw new DomainException(
                "Time entry units must not be negative.",
                "timeimport.units_negative");
        }

        return new TimeEntry(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(importId, nameof(importId)),
            Guard.NotEmpty(workerId, nameof(workerId)),
            workDate,
            units,
            componentCode);
    }
}
