namespace GlobalPay.Domain.Modules.Payroll;

public enum PayrollRunStatus
{
    Draft = 0,
    Calculated = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5,
    Cancelled = 6,
}
