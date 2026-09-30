using GlobalPay.Domain.Modules.Payments;

namespace GlobalPay.Application.Abstractions.Persistence;

public interface IPaymentBatchRepository
{
    Task AddAsync(PaymentBatch batch, CancellationToken cancellationToken = default);

    Task<PaymentBatch?> GetByIdAsync(Guid paymentBatchId, CancellationToken cancellationToken = default);

    Task<PaymentBatch?> GetByPayrollRunAsync(Guid payrollRunId, CancellationToken cancellationToken = default);

    Task<PaymentBatch?> GetByProviderReferenceAsync(string providerReference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentBatch>> ListOpenAsync(int limit, CancellationToken cancellationToken = default);
}
