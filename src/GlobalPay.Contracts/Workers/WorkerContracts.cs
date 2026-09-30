namespace GlobalPay.Contracts.Workers;

public sealed record CreateWorkerRequest(
    string ExternalRef,
    string DisplayName,
    string? Email);

public sealed record WorkerResponse(
    Guid Id,
    string ExternalRef,
    string DisplayName,
    string? Email,
    string Status,
    DateTimeOffset CreatedAtUtc);
