using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Modules.Payroll.Commands;

public sealed record SubmitPayrollRunCommand(Guid PayrollRunId) : ICommand<PayrollRunResponse>;

internal sealed class SubmitPayrollRunCommandHandler : ICommandHandler<SubmitPayrollRunCommand, PayrollRunResponse>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public SubmitPayrollRunCommandHandler(
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
        SubmitPayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanManagePayroll)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to submit payroll runs."));
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

        try
        {
            run.Submit(_currentUser.UserId!, _timeProvider.GetUtcNow());
        }
        catch (DomainException exception)
        {
            return Result.Failure<PayrollRunResponse>(exception.ToError("payroll.submit_invalid"));
        }

        await _auditRecorder
            .RecordAsync("payroll.run.submitted", nameof(PayrollRun), run.Id, null, cancellationToken)
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
