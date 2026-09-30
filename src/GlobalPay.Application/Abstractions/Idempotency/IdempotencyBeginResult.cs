namespace GlobalPay.Application.Abstractions.Idempotency;

public sealed record IdempotencyBeginResult(IdempotencyRecord Record, bool Created);
