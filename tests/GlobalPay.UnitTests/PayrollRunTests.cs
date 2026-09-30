using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.UnitTests;

public sealed class PayrollRunTests
{
    private static readonly Currency Usd = Currency.FromCode("USD");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithoutImports_IsRejected()
    {
        var exception = Assert.Throws<DomainException>(() => NewRun([]));

        Assert.Equal("payroll.imports_required", exception.Code);
    }

    [Fact]
    public void Submit_BeforeCalculation_IsRejected()
    {
        var run = NewRun([Guid.NewGuid()]);

        var exception = Assert.Throws<DomainException>(() => run.Submit("admin.user", Now));

        Assert.Equal("payroll.submit_not_allowed", exception.Code);
    }

    [Fact]
    public void Submit_AfterCalculation_MovesToPendingApproval()
    {
        var run = CalculatedRun("admin.user");

        run.Submit("admin.user", Now);

        Assert.Equal(PayrollRunStatus.PendingApproval, run.Status);
    }

    [Fact]
    public void Approve_ByCreator_IsRejected()
    {
        var run = CalculatedRun("admin.user");
        run.Submit("admin.user", Now);

        var exception = Assert.Throws<DomainException>(() => run.Approve("admin.user", Now));

        Assert.Equal("payroll.maker_checker_violation", exception.Code);
        Assert.Equal(PayrollRunStatus.PendingApproval, run.Status);
    }

    [Fact]
    public void Approve_ByAnotherUser_Succeeds()
    {
        var run = CalculatedRun("admin.user");
        run.Submit("admin.user", Now);

        run.Approve("approver.user", Now);

        Assert.Equal(PayrollRunStatus.Approved, run.Status);
        Assert.Equal("approver.user", run.DecidedBy);
    }

    [Fact]
    public void Reject_ByAnotherUser_RecordsReason()
    {
        var run = CalculatedRun("admin.user");
        run.Submit("admin.user", Now);

        run.Reject("approver.user", "Variance too high", Now);

        Assert.Equal(PayrollRunStatus.Rejected, run.Status);
        Assert.Equal("Variance too high", run.DecisionReason);
    }

    [Fact]
    public void Approve_AfterRejection_IsRejected()
    {
        var run = CalculatedRun("admin.user");
        run.Submit("admin.user", Now);
        run.Reject("approver.user", "No", Now);

        var exception = Assert.Throws<DomainException>(() => run.Approve("approver.user", Now));

        Assert.Equal("payroll.decision_not_allowed", exception.Code);
    }

    [Fact]
    public void AllowedActions_HideApprovalFromCreator()
    {
        var run = CalculatedRun("admin.user");
        run.Submit("admin.user", Now);

        var creatorActions = run.AllowedActions("admin.user", canManage: true, canApprove: true, canPay: false);
        var approverActions = run.AllowedActions("approver.user", canManage: false, canApprove: true, canPay: false);

        Assert.DoesNotContain(PayrollActions.Approve, creatorActions);
        Assert.Contains(PayrollActions.Approve, approverActions);
    }

    private static PayrollRun NewRun(IReadOnlyCollection<Guid> importIds) =>
        PayrollRun.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            Usd,
            importIds,
            "admin.user",
            null,
            Now);

    private static PayrollRun CalculatedRun(string createdBy)
    {
        var run = PayrollRun.Create(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            Usd,
            [Guid.NewGuid()],
            createdBy,
            null,
            Now);

        var version = PayrollRunVersion.Create(run.TenantId, run.Id, 1, PayrollCalculator.RuleVersion, Usd, Now);
        version.AddPayment(Guid.NewGuid(), Guid.NewGuid(), 1000m, 100m, 900m, []);
        version.Seal();

        run.AttachVersion(version);

        return run;
    }
}
