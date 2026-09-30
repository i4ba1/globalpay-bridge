namespace GlobalPay.Contracts.TimeImports;

public sealed record RegisterTimesheetImportRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string FileName,
    string Sha256,
    long SizeBytes);

public sealed record TimesheetImportRegistrationResponse(
    Guid ImportId,
    string BlobName,
    Uri UploadUri,
    DateTimeOffset UploadExpiresAtUtc);

public sealed record TimesheetImportRowErrorResponse(
    int LineNumber,
    string Code,
    string Message);

public sealed record TimesheetImportResponse(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    string? FailureReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ImportProcessingResultResponse(
    Guid ImportId,
    string Status,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<TimesheetImportRowErrorResponse> Errors);
