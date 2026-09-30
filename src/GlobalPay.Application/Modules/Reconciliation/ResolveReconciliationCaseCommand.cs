using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Reconciliation;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Reconciliation;

namespace GlobalPay.Application.Modules.Reconciliation;

public sealed record ResolveReconciliationCaseCommand(
    Guid CaseId,
    bool WriteOff,
    string Note) : ICommand<ReconciliationCaseResponse>;

internal sealed class ResolveReconciliationCaseCommandHandler
    : ICommandHandler<ResolveReconciliationCaseCommand, ReconciliationCaseResponse>
{
    private readonly IReconciliationCaseRepository _reconciliationCases;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ResolveReconciliationCaseCommandHandler(
        IReconciliationCaseRepository reconciliationCases,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _reconciliationCases = reconciliationCases;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ReconciliationCaseResponse>> HandleAsync(
        ResolveReconciliationCaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<ReconciliationCaseResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanPay)
        {
            return Result.Failure<ReconciliationCaseResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to resolve reconciliation cases."));
        }

        var reconciliationCase = await _reconciliationCases
            .GetByIdAsync(command.CaseId, cancellationToken)
            .ConfigureAwait(false);

        if (reconciliationCase is null)
        {
            return Result.Failure<ReconciliationCaseResponse>(
                Error.NotFound("reconciliation.case_not_found", $"Reconciliation case '{command.CaseId}' was not found."));
        }

        var now = _timeProvider.GetUtcNow();

        try
        {
            if (!string.IsNullOrWhiteSpace(_currentUser.UserId))
            {
                reconciliationCase.Assign(_currentUser.UserId!);
            }

            if (command.WriteOff)
            {
                reconciliationCase.WriteOff(command.Note, now);
            }
            else
            {
                reconciliationCase.Resolve(command.Note, now);
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<ReconciliationCaseResponse>(exception.ToError("reconciliation.resolve_invalid"));
        }

        await _auditRecorder
            .RecordAsync(
                command.WriteOff ? "reconciliation.case.written_off" : "reconciliation.case.resolved",
                nameof(ReconciliationCase),
                reconciliationCase.Id,
                command.Note,
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ReconciliationMapper.ToResponse(reconciliationCase);
    }
}
