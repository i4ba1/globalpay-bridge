namespace GlobalPay.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    int Count);

public sealed record ProblemResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail,
    string? CorrelationId,
    IReadOnlyDictionary<string, string[]>? Errors);
