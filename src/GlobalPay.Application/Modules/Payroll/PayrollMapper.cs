using GlobalPay.Contracts.Payroll;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.Application.Modules.Payroll;

internal static class PayrollMapper
{
    public static PayrollRunResponse ToResponse(
        PayrollRun run,
        PayrollRunVersion? version,
        IReadOnlyCollection<string> allowedActions)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(allowedActions);

        var totals = new PayrollTotalsResponse(
            version?.WorkerCount ?? 0,
            version?.GrossTotal ?? 0m,
            version?.DeductionTotal ?? 0m,
            version?.NetTotal ?? 0m);

        var payments = version is null
            ? []
            : version.Payments
                .OrderBy(payment => payment.WorkerId)
                .Select(payment => new WorkerPaymentResponse(
                    payment.WorkerId,
                    payment.ContractId,
                    payment.Gross,
                    payment.Deduction,
                    payment.Net))
                .ToArray();

        return new PayrollRunResponse(
            run.Id,
            run.PeriodStart,
            run.PeriodEnd,
            run.Currency.Code,
            run.Status.ToString(),
            run.CurrentVersion,
            version?.RuleVersion,
            totals,
            allowedActions.ToArray(),
            run.CreatedBy,
            run.CreatedAtUtc,
            run.SubmittedAtUtc,
            run.DecidedAtUtc,
            run.DecidedBy,
            run.DecisionReason,
            run.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(run.RowVersion),
            payments);
    }
}
