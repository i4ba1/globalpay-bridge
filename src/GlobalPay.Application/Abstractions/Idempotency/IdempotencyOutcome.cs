namespace GlobalPay.Application.Abstractions.Idempotency;

public enum IdempotencyOutcome
{
    InProgress = 0,
    Completed = 1,
    Failed = 2,
}
