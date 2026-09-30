namespace GlobalPay.Contracts.Payroll;

public sealed record CreatePayrollRunRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    IReadOnlyList<Guid> SourceImportIds,
    string? Note);

public sealed record PayrollDecisionRequest(
    bool Approve,
    string? Reason);

public sealed record PayrollTotalsResponse(
    int WorkerCount,
    decimal Gross,
    decimal Deduction,
    decimal Net);

public sealed record WorkerPaymentResponse(
    Guid WorkerId,
    Guid ContractId,
    decimal Gross,
    decimal Deduction,
    decimal Net);

public sealed record PayrollRunResponse(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    string Status,
    int CalculationVersion,
    string? RuleVersion,
    PayrollTotalsResponse Totals,
    IReadOnlyList<string> AllowedActions,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecidedBy,
    string? DecisionReason,
    string ETag,
    IReadOnlyList<WorkerPaymentResponse> Payments);

public sealed record PayrollVarianceLineResponse(
    Guid WorkerId,
    string WorkerExternalRef,
    string WorkerName,
    decimal CurrentNet,
    decimal PreviousNet,
    decimal Variance,
    decimal VariancePercent);

public sealed record PayrollVarianceResponse(
    Guid PayrollRunId,
    int Version,
    string Currency,
    decimal CurrentNetTotal,
    decimal PreviousNetTotal,
    decimal VarianceTotal,
    IReadOnlyList<PayrollVarianceLineResponse> Lines);
