using System.Text.Json;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Payments;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payments;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payments;
using Microsoft.Extensions.Logging;

namespace GlobalPay.Application.Modules.Payments;

public sealed record ProcessPaymentWebhookCommand(
    string EventId,
    long Timestamp,
    string Signature,
    string RawBody) : ICommand<Unit>;

internal sealed class ProcessPaymentWebhookCommandHandler
    : ICommandHandler<ProcessPaymentWebhookCommand, Unit>
{
    public const string ProviderCode = "provider-simulator";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IPaymentBatchRepository _paymentBatches;
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IInboxStore _inboxStore;
    private readonly ITenantContextMutator _tenantMutator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPaymentWebhookCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ProcessPaymentWebhookCommandHandler(
        IPaymentBatchRepository paymentBatches,
        IPayrollRunRepository payrollRuns,
        IWebhookSignatureVerifier signatureVerifier,
        IInboxStore inboxStore,
        ITenantContextMutator tenantMutator,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentWebhookCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _paymentBatches = paymentBatches;
        _payrollRuns = payrollRuns;
        _signatureVerifier = signatureVerifier;
        _inboxStore = inboxStore;
        _tenantMutator = tenantMutator;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<Result<Unit>> HandleAsync(
        ProcessPaymentWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rawBody = System.Text.Encoding.UTF8.GetBytes(command.RawBody);

        if (!_signatureVerifier.Verify(command.Signature, command.Timestamp, rawBody))
        {
            return Result.Failure<Unit>(
                Error.Unauthorized("webhook.signature_invalid", "The webhook signature is invalid or expired."));
        }

        PaymentProviderWebhookPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<PaymentProviderWebhookPayload>(command.RawBody, SerializerOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<Unit>(Error.Validation("webhook.payload_invalid", "The webhook payload is not valid JSON."));
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.PaymentReference))
        {
            return Result.Failure<Unit>(Error.Validation("webhook.payload_invalid", "The webhook payload is incomplete."));
        }

        if (!TryMapEventType(payload.EventType, out var eventType))
        {
            return Result.Failure<Unit>(
                Error.Validation("webhook.event_type_unknown", $"Event type '{payload.EventType}' is not supported."));
        }

        var batch = await _paymentBatches
            .GetByProviderReferenceAsync(payload.PaymentReference, cancellationToken)
            .ConfigureAwait(false);

        if (batch is null)
        {
            return Result.Failure<Unit>(
                Error.NotFound("payments.batch_not_found", $"No payment batch matches reference '{payload.PaymentReference}'."));
        }

        _tenantMutator.SetTenant(batch.TenantId);

        var payloadHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(rawBody)).ToLowerInvariant();

        var added = await _inboxStore
            .TryAddAsync(ProviderCode, command.EventId, payloadHash, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (!added)
        {
            _logger.LogInformation("Webhook event {EventId} was already processed; acknowledged without mutation", command.EventId);

            return Unit.Value;
        }

        var applied = 0;

        foreach (var instruction in batch.Instructions)
        {
            if (batch.ApplyProviderEvent(instruction.Id, eventType, payload.PaymentReference, payload.EventType, payload.OccurredAt))
            {
                applied++;
            }
        }

        if (batch.Status == PaymentBatchStatus.Settled)
        {
            var run = await _payrollRuns.GetByIdAsync(batch.PayrollRunId, cancellationToken).ConfigureAwait(false);

            if (run is not null && run.Status == GlobalPay.Domain.Modules.Payroll.PayrollRunStatus.Approved)
            {
                run.MarkPaid(_timeProvider.GetUtcNow());

                await _auditRecorder
                    .RecordAsync("payroll.run.paid", nameof(batch), run.Id, batch.Id.ToString("D"), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await _auditRecorder
            .RecordAsync(
                "payments.webhook.applied",
                nameof(PaymentBatch),
                batch.Id,
                $"{payload.EventType};applied={applied};status={batch.Status}",
                cancellationToken)
            .ConfigureAwait(false);

        await _inboxStore
            .MarkProcessedAsync(ProviderCode, command.EventId, _timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }

    private static bool TryMapEventType(string eventType, out ProviderEventType mapped)
    {
        mapped = eventType switch
        {
            PaymentProviderEventTypes.Accepted => ProviderEventType.Accepted,
            PaymentProviderEventTypes.Settled => ProviderEventType.Settled,
            PaymentProviderEventTypes.Failed => ProviderEventType.Failed,
            _ => (ProviderEventType)(-1),
        };

        return (int)mapped >= 0;
    }
}
