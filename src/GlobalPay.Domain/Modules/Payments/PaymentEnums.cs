namespace GlobalPay.Domain.Modules.Payments;

public enum PaymentInstructionStatus
{
    Pending = 0,
    Submitted = 1,
    Accepted = 2,
    Settled = 3,
    Failed = 4,
}

public enum PaymentBatchStatus
{
    Created = 0,
    Submitted = 1,
    PartiallySettled = 2,
    Settled = 3,
    Failed = 4,
}

public enum ProviderEventType
{
    Accepted = 0,
    Failed = 1,
    Settled = 2,
}
