using GlobalPay.Application.Abstractions;

namespace GlobalPay.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextMutator
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public void SetTenant(Guid? tenantId) => TenantId = tenantId == Guid.Empty ? null : tenantId;
}
