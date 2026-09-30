using GlobalPay.Application.Abstractions.Validation;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Modules.Workforce.Validation;

public sealed class CreateWorkerRequestValidator : IRequestValidator<CreateWorkerRequest>
{
    public ValidationResult Validate(CreateWorkerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(request.ExternalRef))
        {
            errors.Add(new(nameof(request.ExternalRef), "worker.external_ref_required", "External reference is required."));
        }
        else if (request.ExternalRef.Length > 100)
        {
            errors.Add(new(nameof(request.ExternalRef), "worker.external_ref_too_long", "External reference must not exceed 100 characters."));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add(new(nameof(request.DisplayName), "worker.display_name_required", "Display name is required."));
        }
        else if (request.DisplayName.Length > 200)
        {
            errors.Add(new(nameof(request.DisplayName), "worker.display_name_too_long", "Display name must not exceed 200 characters."));
        }

        if (request.Email is { Length: > 320 })
        {
            errors.Add(new(nameof(request.Email), "worker.email_too_long", "Email must not exceed 320 characters."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

public sealed class CreateContractRequestValidator : IRequestValidator<CreateContractRequest>
{
    public ValidationResult Validate(CreateContractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<ValidationError>();

        if (request.EndDate is not null && request.EndDate < request.StartDate)
        {
            errors.Add(new(nameof(request.EndDate), "contract.dates_invalid", "End date must not be before the start date."));
        }

        if (!Currency.IsSupported(request.Currency))
        {
            errors.Add(new(nameof(request.Currency), "contract.currency_unsupported", $"Currency '{request.Currency}' is not supported."));
        }

        if (request.Components is null || request.Components.Count == 0)
        {
            errors.Add(new(nameof(request.Components), "contract.components_required", "At least one compensation component is required."));
            return ValidationResult.Failure(errors);
        }

        for (var index = 0; index < request.Components.Count; index++)
        {
            var component = request.Components[index];

            if (string.IsNullOrWhiteSpace(component.Code))
            {
                errors.Add(new($"Components[{index}].Code", "contract.component_code_required", "Component code is required."));
            }

            if (!Enum.TryParse<CompensationType>(component.Type, ignoreCase: true, out _))
            {
                errors.Add(new($"Components[{index}].Type", "contract.component_type_invalid", $"Component type '{component.Type}' is invalid."));
            }

            if (!Enum.TryParse<CalculationRule>(component.Rule, ignoreCase: true, out _))
            {
                errors.Add(new($"Components[{index}].Rule", "contract.component_rule_invalid", $"Calculation rule '{component.Rule}' is invalid."));
            }

            if (component.Amount < 0m)
            {
                errors.Add(new($"Components[{index}].Amount", "contract.component_amount_negative", "Component amount must not be negative."));
            }

            if (string.Equals(component.Rule, nameof(CalculationRule.PercentOfGross), StringComparison.OrdinalIgnoreCase)
                && component.Amount > 100m)
            {
                errors.Add(new($"Components[{index}].Amount", "contract.component_percent_out_of_range", "Percentage deductions must be between 0 and 100."));
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
