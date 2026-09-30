using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Modules.Payroll.Commands;

public sealed record DecidePayrollRunCommand(
    Guid PayrollRunId,
    bool Approve,
    string? Reason) : ICommand<PayrollRunResponse>;

internal sealed class DecidePayrollRunCommandHandler : ICommandHandler<DecidePayrollRunCommand, PayrollRunResponse>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public DecidePayrollRunCommandHandler(
        IPayrollRunRepository payrollRuns,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _payrollRuns = payrollRuns;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PayrollRunResponse>> HandleAsync(
        DecidePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanApprovePayroll)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to decide payroll runs."));
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Unauthorized("auth.user_required", "An authenticated user is required."));
        }

        var run = await _payrollRuns.GetByIdAsync(command.PayrollRunId, cancellationToken).ConfigureAwait(false);

        if (run is null)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.NotFound("payroll.not_found", $"Payroll run '{command.PayrollRunId}' was not found."));
        }

        var now = _timeProvider.GetUtcNow();

        try
        {
            if (command.Approve)
            {
                run.Approve(_currentUser.UserId!, now);
            }
            else
            {
                run.Reject(_currentUser.UserId!, command.Reason ?? string.Empty, now);
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<PayrollRunResponse>(exception.ToError("payroll.decision_invalid"));
        }

        await _auditRecorder
            .RecordAsync(
                command.Approve ? "payroll.run.approved" : "payroll.run.rejected",
                nameof(PayrollRun),
                run.Id,
                command.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var version = await _payrollRuns
            .GetLatestVersionWithPaymentsAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);

        return PayrollMapper.ToResponse(run, version, run.AllowedActions(
            _currentUser.UserId!,
            _accessPolicy.CanManagePayroll,
            _accessPolicy.CanApprovePayroll,
            _accessPolicy.CanPay));
    }
}
