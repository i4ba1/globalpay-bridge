namespace GlobalPay.Application.Abstractions.Payments;

public sealed record PaymentInstructionSubmission(Guid InstructionId, string WorkerReference, decimal Amount);

public sealed record PaymentSubmission(
    Guid PaymentBatchId,
    string CurrencyCode,
    decimal TotalAmount,
    IReadOnlyList<PaymentInstructionSubmission> Instructions);

public sealed record PaymentSubmissionResult(
    bool Accepted,
    string? ProviderReference,
    string? FailureCode,
    string? FailureMessage);

public sealed record PaymentStatusResult(
    string Status,
    string? ProviderReference,
    string? FailureCode);

public interface IPaymentProvider
{
    Task<PaymentSubmissionResult> SubmitAsync(
        PaymentSubmission submission,
        CancellationToken cancellationToken = default);

    Task<PaymentStatusResult> GetStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default);
}
