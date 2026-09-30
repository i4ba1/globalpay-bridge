using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Domain.Modules.TimeImport;

namespace GlobalPay.Application.Modules.Payroll.Commands;

public sealed record CreatePayrollRunCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Currency,
    IReadOnlyList<Guid> SourceImportIds,
    string? Note) : ICommand<PayrollRunResponse>;

internal sealed class CreatePayrollRunCommandHandler : ICommandHandler<CreatePayrollRunCommand, PayrollRunResponse>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly ITimesheetImportRepository _imports;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreatePayrollRunCommandHandler(
        IPayrollRunRepository payrollRuns,
        ITimesheetImportRepository imports,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _payrollRuns = payrollRuns;
        _imports = imports;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PayrollRunResponse>> HandleAsync(
        CreatePayrollRunCommand command,
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
                Error.Forbidden("auth.forbidden", "You are not allowed to create payroll runs."));
        }

        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Unauthorized("auth.user_required", "An authenticated user is required."));
        }

        foreach (var importId in command.SourceImportIds)
        {
            var import = await _imports.GetByIdAsync(importId, cancellationToken).ConfigureAwait(false);

            if (import is null)
            {
                return Result.Failure<PayrollRunResponse>(
                    Error.NotFound("import.not_found", $"Import '{importId}' was not found."));
            }

            if (import.Status != ImportStatus.Completed)
            {
                return Result.Failure<PayrollRunResponse>(
                    Error.Conflict("payroll.import_not_completed", $"Import '{importId}' is not completed."));
            }
        }

        var run = PayrollRun.Create(
            _tenantContext.TenantId!.Value,
            command.PeriodStart,
            command.PeriodEnd,
            Currency.FromCode(command.Currency),
            command.SourceImportIds,
            _currentUser.UserId!,
            command.Note,
            _timeProvider.GetUtcNow());

        await _payrollRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);

        await _auditRecorder
            .RecordAsync("payroll.run.created", nameof(PayrollRun), run.Id, null, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PayrollMapper.ToResponse(run, null, run.AllowedActions(
            _currentUser.UserId!,
            _accessPolicy.CanManagePayroll,
            _accessPolicy.CanApprovePayroll,
            _accessPolicy.CanPay));
    }
}
