using GlobalPay.Application.Abstractions.Validation;
using GlobalPay.Contracts.Reconciliation;

namespace GlobalPay.Application.Modules.Reconciliation.Validation;

public sealed class ResolveReconciliationCaseRequestValidator : IRequestValidator<ResolveReconciliationCaseRequest>
{
    public ValidationResult Validate(ResolveReconciliationCaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            errors.Add(new(nameof(request.Note), "reconciliation.note_required", "A resolution note is required."));
        }
        else if (request.Note.Length > 1000)
        {
            errors.Add(new(nameof(request.Note), "reconciliation.note_too_long", "The note must not exceed 1000 characters."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
