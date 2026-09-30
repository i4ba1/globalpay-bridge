namespace GlobalPay.Application.Abstractions;

public interface IAccessPolicy
{
    bool CanManageWorkforce { get; }

    bool CanManagePayroll { get; }

    bool CanApprovePayroll { get; }

    bool CanPay { get; }

    bool CanAudit { get; }
}
