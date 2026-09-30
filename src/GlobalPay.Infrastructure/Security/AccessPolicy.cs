using GlobalPay.Application.Abstractions;
using GlobalPay.Domain.Modules.Tenancy;

namespace GlobalPay.Infrastructure.Security;

internal sealed class AccessPolicy : IAccessPolicy
{
    private readonly ICurrentUser _currentUser;

    public AccessPolicy(ICurrentUser currentUser) => _currentUser = currentUser;

    public bool CanManageWorkforce => HasRole(Roles.Admin);

    public bool CanManagePayroll => HasRole(Roles.Admin);

    public bool CanApprovePayroll => HasRole(Roles.Approver);

    public bool CanPay => HasRole(Roles.Finance);

    public bool CanAudit =>
        HasRole(Roles.Auditor) || HasRole(Roles.PlatformOperator) || HasRole(Roles.Admin);

    private bool HasRole(string role) => _currentUser.IsAuthenticated && _currentUser.HasRole(role);
}
