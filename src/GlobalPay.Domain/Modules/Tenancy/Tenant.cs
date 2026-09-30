using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Tenancy;

public sealed class Tenant : AggregateRoot<Guid>
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name, TenantStatus status, DateTimeOffset createdAtUtc)
        : base(id)
    {
        Name = name;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = null!;

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Tenant Create(Guid id, string name, DateTimeOffset createdAtUtc) =>
        new(Guard.NotEmpty(id, nameof(id)), Guard.NotNullOrWhiteSpace(name, nameof(name)), TenantStatus.Active, createdAtUtc);

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;
}
