using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payments;
using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Modules.Payments;

public sealed record GetPaymentBatchQuery(Guid PaymentBatchId) : IQuery<PaymentBatchResponse>;

internal sealed class GetPaymentBatchQueryHandler : IQueryHandler<GetPaymentBatchQuery, PaymentBatchResponse>
{
    private readonly IPaymentBatchRepository _paymentBatches;
    private readonly ITenantContext _tenantContext;

    public GetPaymentBatchQueryHandler(IPaymentBatchRepository paymentBatches, ITenantContext tenantContext)
    {
        _paymentBatches = paymentBatches;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PaymentBatchResponse>> HandleAsync(
        GetPaymentBatchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        var batch = await _paymentBatches.GetByIdAsync(query.PaymentBatchId, cancellationToken).ConfigureAwait(false);

        if (batch is null)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.NotFound("payments.batch_not_found", $"Payment batch '{query.PaymentBatchId}' was not found."));
        }

        return PaymentMapper.ToResponse(batch);
    }
}
