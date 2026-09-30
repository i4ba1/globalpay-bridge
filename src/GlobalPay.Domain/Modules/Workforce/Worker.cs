using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Workforce;

public sealed class Worker : AggregateRoot<Guid>, ITenantScoped
{
    private Worker()
    {
    }

    private Worker(
        Guid id,
        Guid tenantId,
        string externalRef,
        string displayName,
        string? email,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        ExternalRef = externalRef;
        DisplayName = displayName;
        Email = email;
        Status = WorkerStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TenantId { get; private set; }

    public string ExternalRef { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string? Email { get; private set; }

    public WorkerStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Worker Create(
        Guid tenantId,
        string externalRef,
        string displayName,
        string? email,
        DateTimeOffset createdAtUtc) =>
        new(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotNullOrWhiteSpace(externalRef, nameof(externalRef)),
            Guard.NotNullOrWhiteSpace(displayName, nameof(displayName)),
            email,
            createdAtUtc);

    public void Deactivate() => Status = WorkerStatus.Inactive;

    public void Activate() => Status = WorkerStatus.Active;
}
