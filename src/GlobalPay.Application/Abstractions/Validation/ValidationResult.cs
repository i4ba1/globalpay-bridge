namespace GlobalPay.Application.Abstractions.Validation;

public sealed record ValidationError(string Field, string Code, string Message);

public sealed class ValidationResult
{
    private static readonly ValidationResult SuccessResult = new([]);

    private ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success() => SuccessResult;

    public static ValidationResult Failure(IReadOnlyList<ValidationError> errors) =>
        errors.Count == 0 ? SuccessResult : new ValidationResult(errors);
}
