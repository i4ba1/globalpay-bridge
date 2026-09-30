namespace GlobalPay.Contracts.Reconciliation;

public sealed record ReconciliationCaseResponse(
    Guid Id,
    Guid PaymentBatchId,
    Guid PaymentInstructionId,
    string Reason,
    string Detail,
    string Status,
    string? Owner,
    string? ResolutionNote,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record ResolveReconciliationCaseRequest(
    bool WriteOff,
    string Note);
