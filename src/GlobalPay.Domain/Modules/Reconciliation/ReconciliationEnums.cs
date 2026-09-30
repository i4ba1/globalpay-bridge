namespace GlobalPay.Domain.Modules.Reconciliation;

public enum ReconciliationCaseStatus
{
    Open = 0,
    Investigating = 1,
    Resolved = 2,
    WrittenOff = 3,
}

public enum ReconciliationReason
{
    MissingConfirmation = 0,
    AmountMismatch = 1,
    PaymentFailed = 2,
}
