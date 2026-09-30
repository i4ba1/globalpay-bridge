using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Modules.Payroll.Commands;

public sealed record CalculatePayrollRunCommand(Guid PayrollRunId) : ICommand<PayrollRunResponse>;

internal sealed class CalculatePayrollRunCommandHandler
    : ICommandHandler<CalculatePayrollRunCommand, PayrollRunResponse>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly IContractRepository _contracts;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CalculatePayrollRunCommandHandler(
        IPayrollRunRepository payrollRuns,
        IContractRepository contracts,
        ITimeEntryRepository timeEntries,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _payrollRuns = payrollRuns;
        _contracts = contracts;
        _timeEntries = timeEntries;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PayrollRunResponse>> HandleAsync(
        CalculatePayrollRunCommand command,
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
                Error.Forbidden("auth.forbidden", "You are not allowed to calculate payroll runs."));
        }

        var run = await _payrollRuns.GetByIdAsync(command.PayrollRunId, cancellationToken).ConfigureAwait(false);

        if (run is null)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.NotFound("payroll.not_found", $"Payroll run '{command.PayrollRunId}' was not found."));
        }

        if (run.Status is PayrollRunStatus.PendingApproval or PayrollRunStatus.Approved or PayrollRunStatus.Paid)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Conflict("payroll.recalculate_not_allowed", $"A payroll run in status '{run.Status}' cannot be recalculated."));
        }

        var workerIds = await _timeEntries
            .GetWorkerIdsForImportsAsync(run.SourceImportIds, cancellationToken)
            .ConfigureAwait(false);

        var unitsByWorker = await _timeEntries
            .SumUnitsByWorkerForImportsAsync(run.SourceImportIds, cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow();
        var version = PayrollRunVersion.Create(
            run.TenantId,
            run.Id,
            run.CurrentVersion + 1,
            PayrollCalculator.RuleVersion,
            run.Currency,
            now);

        foreach (var workerId in workerIds.OrderBy(id => id))
        {
            var contract = await _contracts
                .GetEffectiveContractAsync(workerId, run.PeriodEnd, cancellationToken)
                .ConfigureAwait(false);

            if (contract is null)
            {
                continue;
            }

            unitsByWorker.TryGetValue(workerId, out var units);

            var components = contract.Components
                .Select(component => new PayrollComponentInput(
                    component.Code,
                    component.Type,
                    component.Rule,
                    component.Amount))
                .ToArray();

            var result = PayrollCalculator.Calculate(
                new PayrollWorkerInput(workerId, contract.Id, run.Currency, units, components));

            var lines = result.Lines
                .Select(line => new PaymentLineSpec(line.ComponentCode, line.Type, line.Rule, line.Amount))
                .ToArray();

            version.AddPayment(workerId, contract.Id, result.Gross, result.Deduction, result.Net, lines);
        }

        version.Seal();

        await _payrollRuns.AddVersionAsync(version, cancellationToken).ConfigureAwait(false);

        try
        {
            run.AttachVersion(version);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PayrollRunResponse>(exception.ToError("payroll.recalculate_not_allowed"));
        }

        await _auditRecorder
            .RecordAsync(
                "payroll.run.calculated",
                nameof(PayrollRun),
                run.Id,
                $"version={version.Version};workers={version.WorkerCount};net={version.NetTotal}",
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PayrollMapper.ToResponse(run, version, run.AllowedActions(
            _currentUser.UserId ?? string.Empty,
            _accessPolicy.CanManagePayroll,
            _accessPolicy.CanApprovePayroll,
            _accessPolicy.CanPay));
    }
}
