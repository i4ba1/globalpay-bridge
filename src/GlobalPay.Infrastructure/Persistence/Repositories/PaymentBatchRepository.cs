using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Payments;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence.Repositories;

internal sealed class PaymentBatchRepository : IPaymentBatchRepository
{
    private readonly GlobalPayDbContext _context;

    public PaymentBatchRepository(GlobalPayDbContext context) => _context = context;

    public async Task AddAsync(PaymentBatch batch, CancellationToken cancellationToken = default) =>
        await _context.PaymentBatches.AddAsync(batch, cancellationToken).ConfigureAwait(false);

    public Task<PaymentBatch?> GetByIdAsync(Guid paymentBatchId, CancellationToken cancellationToken = default) =>
        _context.PaymentBatches
            .Include(batch => batch.Instructions)
            .FirstOrDefaultAsync(batch => batch.Id == paymentBatchId, cancellationToken);

    public Task<PaymentBatch?> GetByPayrollRunAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
        _context.PaymentBatches
            .Include(batch => batch.Instructions)
            .FirstOrDefaultAsync(batch => batch.PayrollRunId == payrollRunId, cancellationToken);

    public Task<PaymentBatch?> GetByProviderReferenceAsync(
        string providerReference,
        CancellationToken cancellationToken = default) =>
        _context.PaymentBatches
            .Include(batch => batch.Instructions)
            .FirstOrDefaultAsync(batch => batch.ProviderReference == providerReference, cancellationToken);

    public async Task<IReadOnlyList<PaymentBatch>> ListOpenAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await _context.PaymentBatches
            .Include(batch => batch.Instructions)
            .Where(batch => batch.Status == PaymentBatchStatus.Submitted
                            || batch.Status == PaymentBatchStatus.PartiallySettled)
            .OrderBy(batch => batch.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
