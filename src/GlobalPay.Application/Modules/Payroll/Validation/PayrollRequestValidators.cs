using GlobalPay.Application.Abstractions.Validation;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Application.Modules.Payroll.Validation;

public sealed class CreatePayrollRunRequestValidator : IRequestValidator<CreatePayrollRunRequest>
{
    public ValidationResult Validate(CreatePayrollRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (request.PeriodEnd < request.PeriodStart)
        {
            errors.Add(new(nameof(request.PeriodEnd), "payroll.period_invalid", "Period end must not be before the period start."));
        }

        if (!Currency.IsSupported(request.Currency))
        {
            errors.Add(new(nameof(request.Currency), "payroll.currency_unsupported", $"Currency '{request.Currency}' is not supported."));
        }

        if (request.SourceImportIds is null || request.SourceImportIds.Count == 0)
        {
            errors.Add(new(nameof(request.SourceImportIds), "payroll.imports_required", "At least one completed import is required."));
        }

        if (request.Note is { Length: > 500 })
        {
            errors.Add(new(nameof(request.Note), "payroll.note_too_long", "Note must not exceed 500 characters."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

public sealed class PayrollDecisionRequestValidator : IRequestValidator<PayrollDecisionRequest>
{
    public ValidationResult Validate(PayrollDecisionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add(new(nameof(request.Reason), "payroll.reason_required", "A reason is required when rejecting."));
        }

        if (request.Reason is { Length: > 500 })
        {
            errors.Add(new(nameof(request.Reason), "payroll.reason_too_long", "Reason must not exceed 500 characters."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
