namespace GlobalPay.Application.Abstractions.TimeImport;

public sealed record TimesheetRow(
    int LineNumber,
    string WorkerExternalRef,
    DateOnly WorkDate,
    decimal Units,
    string? ComponentCode);

public sealed record TimesheetParseError(
    int LineNumber,
    string Code,
    string Message);

public sealed record TimesheetParseResult(
    IReadOnlyList<TimesheetRow> Rows,
    IReadOnlyList<TimesheetParseError> Errors,
    int TotalDataRows);

public interface ITimesheetCsvParser
{
    TimesheetParseResult Parse(Stream content);
}
