namespace GlobalPay.Application.Abstractions.Idempotency;

public sealed record IdempotencyRecord(
    Guid Id,
    Guid TenantId,
    string OperationCode,
    string IdempotencyKey,
    string RequestFingerprint,
    IdempotencyOutcome Outcome,
    int? StatusCode,
    string? ResponseBody,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
