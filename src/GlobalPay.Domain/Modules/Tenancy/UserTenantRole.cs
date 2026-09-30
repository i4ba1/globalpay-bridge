using GlobalPay.Domain.Common;

namespace GlobalPay.Domain.Modules.Tenancy;

public sealed class UserTenantRole : Entity<Guid>
{
    private UserTenantRole()
    {
    }

    private UserTenantRole(Guid id, string userId, Guid tenantId, string roleCode)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        RoleCode = roleCode;
    }

    public string UserId { get; private set; } = null!;

    public Guid TenantId { get; private set; }

    public string RoleCode { get; private set; } = null!;

    public static UserTenantRole Assign(string userId, Guid tenantId, string roleCode) =>
        new(
            Guid.NewGuid(),
            Guard.NotNullOrWhiteSpace(userId, nameof(userId)),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotNullOrWhiteSpace(roleCode, nameof(roleCode)));
}
