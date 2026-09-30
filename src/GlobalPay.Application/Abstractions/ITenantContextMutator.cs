namespace GlobalPay.Application.Abstractions;

public interface ITenantContextMutator
{
    void SetTenant(Guid? tenantId);
}
