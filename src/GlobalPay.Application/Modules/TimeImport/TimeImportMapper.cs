using GlobalPay.Contracts.TimeImports;
using GlobalPay.Domain.Modules.TimeImport;

namespace GlobalPay.Application.Modules.TimeImport;

internal static class TimeImportMapper
{
    public static TimesheetImportResponse ToResponse(TimesheetImport import)
    {
        ArgumentNullException.ThrowIfNull(import);

        return new TimesheetImportResponse(
            import.Id,
            import.PeriodStart,
            import.PeriodEnd,
            import.Status.ToString(),
            import.TotalRows,
            import.ValidRows,
            import.InvalidRows,
            import.FailureReason,
            import.CreatedAtUtc,
            import.CompletedAtUtc);
    }
}
