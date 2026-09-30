namespace GlobalPay.Contracts.Payments;

public sealed record PaymentInstructionResponse(
    Guid Id,
    Guid WorkerId,
    decimal Amount,
    string Status,
    string? ProviderReference,
    string? FailureCode,
    DateTimeOffset? LastEventAtUtc);

public sealed record PaymentBatchResponse(
    Guid Id,
    Guid PayrollRunId,
    string Currency,
    string Status,
    decimal TotalAmount,
    string IdempotencyKey,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? SettledAtUtc,
    IReadOnlyList<PaymentInstructionResponse> Instructions);
