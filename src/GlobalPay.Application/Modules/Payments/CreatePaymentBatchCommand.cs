using System.Text.Json;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Idempotency;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payments;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payments;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Modules.Payments;

internal static class PaymentMapper
{
    public static PaymentBatchResponse ToResponse(PaymentBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new PaymentBatchResponse(
            batch.Id,
            batch.PayrollRunId,
            batch.Currency.Code,
            batch.Status.ToString(),
            batch.TotalAmount,
            batch.IdempotencyKey,
            batch.ProviderReference,
            batch.CreatedAtUtc,
            batch.SubmittedAtUtc,
            batch.SettledAtUtc,
            batch.Instructions
                .OrderBy(instruction => instruction.WorkerId)
                .Select(instruction => new PaymentInstructionResponse(
                    instruction.Id,
                    instruction.WorkerId,
                    instruction.Amount,
                    instruction.Status.ToString(),
                    instruction.ProviderReference,
                    instruction.FailureCode,
                    instruction.LastEventAtUtc))
                .ToArray());
    }
}

public sealed record CreatePaymentBatchCommand(Guid PayrollRunId, string IdempotencyKey) : ICommand<PaymentBatchResponse>;

internal sealed class CreatePaymentBatchCommandHandler : ICommandHandler<CreatePaymentBatchCommand, PaymentBatchResponse>
{
    public const string OperationCode = "payments.create-batch";

    private readonly IPayrollRunRepository _payrollRuns;
    private readonly IPaymentBatchRepository _paymentBatches;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreatePaymentBatchCommandHandler(
        IPayrollRunRepository payrollRuns,
        IPaymentBatchRepository paymentBatches,
        IIdempotencyStore idempotencyStore,
        IOutboxWriter outboxWriter,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _payrollRuns = payrollRuns;
        _paymentBatches = paymentBatches;
        _idempotencyStore = idempotencyStore;
        _outboxWriter = outboxWriter;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PaymentBatchResponse>> HandleAsync(
        CreatePaymentBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanPay)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to submit payments."));
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Unauthorized("auth.user_required", "An authenticated user is required."));
        }

        var run = await _payrollRuns.GetByIdAsync(command.PayrollRunId, cancellationToken).ConfigureAwait(false);

        if (run is null)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.NotFound("payroll.not_found", $"Payroll run '{command.PayrollRunId}' was not found."));
        }

        var existingBatch = await _paymentBatches
            .GetByPayrollRunAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingBatch is not null)
        {
            return PaymentMapper.ToResponse(existingBatch);
        }

        if (run.Status != PayrollRunStatus.Approved)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Conflict("payments.run_not_approved", $"A payment batch requires an approved run; current status is '{run.Status}'."));
        }

        var version = await _payrollRuns
            .GetLatestVersionWithPaymentsAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Conflict("payments.no_version", "The payroll run has no calculated version."));
        }

        var instructions = version.Payments
            .Where(payment => payment.Net > 0m)
            .Select(payment => new PaymentInstructionSpec(payment.Id, payment.WorkerId, payment.Net))
            .ToArray();

        if (instructions.Length == 0)
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Conflict("payments.no_payable_workers", "The payroll run has no payable workers."));
        }

        var key = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? run.Id.ToString("D") : command.IdempotencyKey.Trim();
        var fingerprint = $"{run.Id:D}:{version.Id:D}";

        var idempotency = await _idempotencyStore
            .FindAsync(run.TenantId, OperationCode, key, cancellationToken)
            .ConfigureAwait(false);

        if (idempotency is not null)
        {
            return ResolveReplay(idempotency, fingerprint);
        }

        var begin = await _idempotencyStore
            .BeginAsync(run.TenantId, OperationCode, key, fingerprint, cancellationToken)
            .ConfigureAwait(false);

        if (!begin.Created)
        {
            return ResolveReplay(begin.Record, fingerprint);
        }

        var batch = PaymentBatch.Create(
            run.TenantId,
            run.Id,
            run.Currency,
            key,
            _currentUser.UserId!,
            _timeProvider.GetUtcNow(),
            instructions);

        await _paymentBatches.AddAsync(batch, cancellationToken).ConfigureAwait(false);

        await _outboxWriter
            .EnqueueAsync(
                PaymentsOutboxMessageTypes.BatchDispatch,
                JsonSerializer.Serialize(new PaymentBatchDispatchMessage(batch.Id, batch.TenantId)),
                cancellationToken)
            .ConfigureAwait(false);

        await _auditRecorder
            .RecordAsync(
                "payments.batch.created",
                nameof(PaymentBatch),
                batch.Id,
                $"instructions={batch.Instructions.Count};total={batch.TotalAmount}",
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var response = PaymentMapper.ToResponse(batch);

        await _idempotencyStore
            .CompleteAsync(begin.Record.Id, StatusCodes.Created, JsonSerializer.Serialize(response), cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    private static Result<PaymentBatchResponse> ResolveReplay(IdempotencyRecord record, string fingerprint)
    {
        if (!string.Equals(record.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return Result.Failure<PaymentBatchResponse>(
                Error.Conflict(
                    "idempotency.fingerprint_mismatch",
                    "The idempotency key was used with a different request payload."));
        }

        if (record.Outcome == IdempotencyOutcome.Completed && !string.IsNullOrWhiteSpace(record.ResponseBody))
        {
            var replay = JsonSerializer.Deserialize<PaymentBatchResponse>(record.ResponseBody);

            if (replay is not null)
            {
                return replay;
            }
        }

        return Result.Failure<PaymentBatchResponse>(
            Error.Conflict(
                "idempotency.in_progress",
                "A request with this idempotency key is still in progress."));
    }

    private static class StatusCodes
    {
        public const int Created = 201;
    }
}
