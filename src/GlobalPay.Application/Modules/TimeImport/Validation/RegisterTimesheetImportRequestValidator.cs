using GlobalPay.Application.Abstractions.Validation;
using GlobalPay.Contracts.TimeImports;

namespace GlobalPay.Application.Modules.TimeImport.Validation;

public sealed class RegisterTimesheetImportRequestValidator : IRequestValidator<RegisterTimesheetImportRequest>
{
    private const long MaxSizeBytes = 50 * 1024 * 1024;

    public ValidationResult Validate(RegisterTimesheetImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (request.PeriodEnd < request.PeriodStart)
        {
            errors.Add(new(nameof(request.PeriodEnd), "import.period_invalid", "Period end must not be before the period start."));
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            errors.Add(new(nameof(request.FileName), "import.file_name_required", "File name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Sha256) || request.Sha256.Length != 64)
        {
            errors.Add(new(nameof(request.Sha256), "import.checksum_invalid", "A SHA-256 hex checksum is required."));
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > MaxSizeBytes)
        {
            errors.Add(new(nameof(request.SizeBytes), "import.size_invalid", "File size must be between 1 byte and 50 MB."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
