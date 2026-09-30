namespace GlobalPay.Application.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }
}
