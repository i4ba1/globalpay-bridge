using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payments;
using GlobalPay.Domain.Modules.Reconciliation;

namespace GlobalPay.Application.Modules.Reconciliation;

public sealed record ReconcilePaymentsResult(int Scanned, int Opened, int Existing);

public sealed record ReconcilePaymentsCommand(int BatchLimit) : ICommand<ReconcilePaymentsResult>;

internal sealed class ReconcilePaymentsCommandHandler
    : ICommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult>
{
    private readonly IPaymentBatchRepository _paymentBatches;
    private readonly IReconciliationCaseRepository _reconciliationCases;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReconcilePaymentsCommandHandler(
        IPaymentBatchRepository paymentBatches,
        IReconciliationCaseRepository reconciliationCases,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _paymentBatches = paymentBatches;
        _reconciliationCases = reconciliationCases;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ReconcilePaymentsResult>> HandleAsync(
        ReconcilePaymentsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var batches = await _paymentBatches
            .ListOpenAsync(command.BatchLimit, cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow();
        var opened = 0;
        var existing = 0;

        foreach (var batch in batches)
        {
            foreach (var instruction in batch.Instructions)
            {
                var reason = ResolveReason(instruction.Status);

                if (reason is null)
                {
                    continue;
                }

                if (await _reconciliationCases
                        .ExistsForInstructionAsync(instruction.Id, cancellationToken)
                        .ConfigureAwait(false))
                {
                    existing++;

                    continue;
                }

                var reconciliationCase = ReconciliationCase.Open(
                    batch.TenantId,
                    batch.Id,
                    instruction.Id,
                    reason.Value,
                    BuildDetail(instruction),
                    now);

                await _reconciliationCases.AddAsync(reconciliationCase, cancellationToken).ConfigureAwait(false);

                await _auditRecorder
                    .RecordAsync(
                        "reconciliation.case.opened",
                        nameof(ReconciliationCase),
                        reconciliationCase.Id,
                        $"{reason};instruction={instruction.Id:D}",
                        cancellationToken)
                    .ConfigureAwait(false);

                opened++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ReconcilePaymentsResult(batches.Count, opened, existing);
    }

    private static ReconciliationReason? ResolveReason(PaymentInstructionStatus status) => status switch
    {
        PaymentInstructionStatus.Failed => ReconciliationReason.PaymentFailed,
        PaymentInstructionStatus.Submitted => ReconciliationReason.MissingConfirmation,
        PaymentInstructionStatus.Accepted => ReconciliationReason.MissingConfirmation,
        _ => null,
    };

    private static string BuildDetail(PaymentInstruction instruction) =>
        $"Instruction {instruction.Id:D} is '{instruction.Status}' with amount {instruction.Amount} {instruction.Currency.Code}.";
}
