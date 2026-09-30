namespace GlobalPay.Contracts.Workers;

public sealed record CompensationComponentRequest(
    string Type,
    string Code,
    string Rule,
    decimal Amount);

public sealed record CreateContractRequest(
    DateOnly StartDate,
    DateOnly? EndDate,
    string Currency,
    IReadOnlyList<CompensationComponentRequest> Components);

public sealed record CompensationComponentResponse(
    Guid Id,
    string Type,
    string Code,
    string Rule,
    decimal Amount);

public sealed record ContractResponse(
    Guid Id,
    Guid WorkerId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Currency,
    IReadOnlyList<CompensationComponentResponse> Components,
    DateTimeOffset CreatedAtUtc);
