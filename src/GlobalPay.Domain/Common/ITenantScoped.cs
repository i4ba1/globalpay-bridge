namespace GlobalPay.Domain.Common;

public interface ITenantScoped
{
    Guid TenantId { get; }
}
