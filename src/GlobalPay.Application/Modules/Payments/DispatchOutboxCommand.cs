using System.Text.Json;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Payments;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payments;
using Microsoft.Extensions.Logging;

namespace GlobalPay.Application.Modules.Payments;

public sealed record OutboxDispatchResult(int Leased, int Processed, int Failed);

public sealed record DispatchOutboxCommand(int BatchSize, TimeSpan LeaseDuration)
    : ICommand<OutboxDispatchResult>;

internal sealed class DispatchOutboxCommandHandler : ICommandHandler<DispatchOutboxCommand, OutboxDispatchResult>
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly IOutboxStore _outboxStore;
    private readonly IPaymentBatchRepository _paymentBatches;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DispatchOutboxCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public DispatchOutboxCommandHandler(
        IOutboxStore outboxStore,
        IPaymentBatchRepository paymentBatches,
        IPaymentProvider paymentProvider,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        ILogger<DispatchOutboxCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _outboxStore = outboxStore;
        _paymentBatches = paymentBatches;
        _paymentProvider = paymentProvider;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<Result<OutboxDispatchResult>> HandleAsync(
        DispatchOutboxCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var messages = await _outboxStore
            .LeaseAsync(command.BatchSize, command.LeaseDuration, cancellationToken)
            .ConfigureAwait(false);

        var processed = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            try
            {
                if (string.Equals(message.Type, PaymentsOutboxMessageTypes.BatchDispatch, StringComparison.Ordinal))
                {
                    var handled = await DispatchPaymentBatchAsync(message, cancellationToken).ConfigureAwait(false);

                    if (handled)
                    {
                        await _outboxStore.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                        processed++;

                        continue;
                    }
                }
                else
                {
                    _logger.LogWarning("Quarantining unknown outbox message type {Type} ({Id})", message.Type, message.Id);
                    await _outboxStore.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                    processed++;

                    continue;
                }

                await _outboxStore
                    .MarkFailedAsync(message.Id, "provider.transient", ComputeBackoff(message.Attempts), cancellationToken)
                    .ConfigureAwait(false);
                failed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Outbox dispatch failed for {Id}", message.Id);

                await _outboxStore
                    .MarkFailedAsync(message.Id, exception.Message, ComputeBackoff(message.Attempts), cancellationToken)
                    .ConfigureAwait(false);
                failed++;
            }
        }

        return new OutboxDispatchResult(messages.Count, processed, failed);
    }

    private async Task<bool> DispatchPaymentBatchAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        var dispatch = JsonSerializer.Deserialize<PaymentBatchDispatchMessage>(message.Payload);

        if (dispatch is null)
        {
            _logger.LogWarning("Outbox message {Id} has an unreadable payload", message.Id);

            return true;
        }

        var batch = await _paymentBatches.GetByIdAsync(dispatch.PaymentBatchId, cancellationToken).ConfigureAwait(false);

        if (batch is null)
        {
            _logger.LogWarning("Payment batch {BatchId} no longer exists; dropping outbox message", dispatch.PaymentBatchId);

            return true;
        }

        if (batch.Status != PaymentBatchStatus.Created)
        {
            return true;
        }

        var submission = new PaymentSubmission(
            batch.Id,
            batch.Currency.Code,
            batch.TotalAmount,
            batch.Instructions
                .Select(instruction => new PaymentInstructionSubmission(
                    instruction.Id,
                    instruction.WorkerId.ToString("D"),
                    instruction.Amount))
                .ToArray());

        var result = await _paymentProvider.SubmitAsync(submission, cancellationToken).ConfigureAwait(false);

        if (!result.Accepted)
        {
            if (result.FailureCode is not null && result.FailureCode.StartsWith("provider.http_4", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Payment batch {BatchId} was permanently rejected by the provider: {Code}",
                    batch.Id,
                    result.FailureCode);

                await _auditRecorder
                    .RecordAsync("payments.batch.rejected", nameof(PaymentBatch), batch.Id, result.FailureCode, cancellationToken)
                    .ConfigureAwait(false);

                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return true;
            }

            return false;
        }

        batch.MarkSubmitted(result.ProviderReference, _timeProvider.GetUtcNow());

        await _auditRecorder
            .RecordAsync("payments.batch.submitted", nameof(PaymentBatch), batch.Id, result.ProviderReference, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static TimeSpan ComputeBackoff(int attempts)
    {
        var seconds = Math.Min(Math.Pow(2, Math.Min(attempts, 8)), MaxBackoff.TotalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}
