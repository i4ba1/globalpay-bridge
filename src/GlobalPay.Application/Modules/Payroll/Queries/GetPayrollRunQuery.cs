using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Application.Abstractions.Reporting;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Modules.Payroll.Queries;

public sealed record GetPayrollRunQuery(Guid PayrollRunId) : IQuery<PayrollRunResponse>;

internal sealed class GetPayrollRunQueryHandler : IQueryHandler<GetPayrollRunQuery, PayrollRunResponse>
{
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessPolicy _accessPolicy;

    public GetPayrollRunQueryHandler(
        IPayrollRunRepository payrollRuns,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAccessPolicy accessPolicy)
    {
        _payrollRuns = payrollRuns;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<Result<PayrollRunResponse>> HandleAsync(
        GetPayrollRunQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        var run = await _payrollRuns.GetByIdAsync(query.PayrollRunId, cancellationToken).ConfigureAwait(false);

        if (run is null)
        {
            return Result.Failure<PayrollRunResponse>(
                Error.NotFound("payroll.not_found", $"Payroll run '{query.PayrollRunId}' was not found."));
        }

        var version = await _payrollRuns
            .GetLatestVersionWithPaymentsAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);

        return PayrollMapper.ToResponse(run, version, run.AllowedActions(
            _currentUser.UserId ?? string.Empty,
            _accessPolicy.CanManagePayroll,
            _accessPolicy.CanApprovePayroll,
            _accessPolicy.CanPay));
    }
}

public sealed record GetPayrollVarianceQuery(Guid PayrollRunId) : IQuery<PayrollVarianceResponse>;

internal sealed class GetPayrollVarianceQueryHandler : IQueryHandler<GetPayrollVarianceQuery, PayrollVarianceResponse>
{
    private readonly IPayrollReportingQueries _reportingQueries;
    private readonly ITenantContext _tenantContext;

    public GetPayrollVarianceQueryHandler(
        IPayrollReportingQueries reportingQueries,
        ITenantContext tenantContext)
    {
        _reportingQueries = reportingQueries;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PayrollVarianceResponse>> HandleAsync(
        GetPayrollVarianceQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<PayrollVarianceResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        var report = await _reportingQueries
            .GetVarianceAsync(query.PayrollRunId, cancellationToken)
            .ConfigureAwait(false);

        if (report is null)
        {
            return Result.Failure<PayrollVarianceResponse>(
                Error.NotFound("payroll.variance_not_found", $"No calculated version exists for payroll run '{query.PayrollRunId}'."));
        }

        return new PayrollVarianceResponse(
            report.PayrollRunId,
            report.Version,
            report.CurrencyCode,
            report.CurrentNetTotal,
            report.PreviousNetTotal,
            report.VarianceTotal,
            report.Lines
                .Select(line => new PayrollVarianceLineResponse(
                    line.WorkerId,
                    line.WorkerExternalRef,
                    line.WorkerName,
                    line.CurrentNet,
                    line.PreviousNet,
                    line.Variance,
                    line.VariancePercent))
                .ToArray());
    }
}
