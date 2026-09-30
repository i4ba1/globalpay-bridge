namespace GlobalPay.Application.Abstractions.Validation;

public interface IRequestValidator<in TRequest>
{
    ValidationResult Validate(TRequest request);
}
