using System.Security.Cryptography;
using System.Text;
using GlobalPay.Application.Modules.Payroll.Commands;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Application.Modules.Workforce.Commands;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.UnitTests.Support;

namespace GlobalPay.UnitTests;

public sealed class PayrollFlowTests
{
    private static readonly DateOnly PeriodStart = new(2026, 9, 1);

    private static readonly DateOnly PeriodEnd = new(2026, 9, 30);

    [Fact]
    public async Task HappyPath_ImportCalculateSubmitApprove()
    {
        var tenantId = Guid.NewGuid();
        var harness = new PayrollHarness(tenantId);

        var worker = await CreateWorkerAsync(harness, "W-001");
        await CreateContractAsync(harness, worker.Id);

        var importId = await ImportTimesheetAsync(harness, "W-001", 8m);

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [importId], "September run"));

        Assert.True(created.IsSuccess);
        Assert.Equal(PayrollRunStatus.Draft.ToString(), created.Value.Status);

        var calculated = await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(created.Value.Id));

        Assert.True(calculated.IsSuccess);
        Assert.Equal(200m, calculated.Value.Totals.Gross);
        Assert.Equal(20m, calculated.Value.Totals.Deduction);
        Assert.Equal(180m, calculated.Value.Totals.Net);
        Assert.Equal(1, calculated.Value.Totals.WorkerCount);
        Assert.Equal(PayrollCalculator.RuleVersion, calculated.Value.RuleVersion);
        Assert.Single(calculated.Value.Payments);

        var submitted = await harness.SubmitHandler.HandleAsync(new SubmitPayrollRunCommand(created.Value.Id));

        Assert.True(submitted.IsSuccess);
        Assert.Equal(PayrollRunStatus.PendingApproval.ToString(), submitted.Value.Status);

        harness.User.UserId = "approver.user";

        var approved = await harness.DecideHandler.HandleAsync(
            new DecidePayrollRunCommand(created.Value.Id, Approve: true, Reason: null));

        Assert.True(approved.IsSuccess);
        Assert.Equal(PayrollRunStatus.Approved.ToString(), approved.Value.Status);
        Assert.Equal("approver.user", approved.Value.DecidedBy);
    }

    [Fact]
    public async Task Calculate_IsDeterministic_AcrossRepeatedRuns()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await CreateWorkerAsync(harness, "W-002");
        await CreateContractAsync(harness, worker.Id);
        var importId = await ImportTimesheetAsync(harness, "W-002", 12.5m);

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [importId], null));

        var first = await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(created.Value.Id));
        var second = await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(created.Value.Id));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Totals.Gross, second.Value.Totals.Gross);
        Assert.Equal(first.Value.Totals.Net, second.Value.Totals.Net);
        Assert.Equal(2, second.Value.CalculationVersion);
    }

    [Fact]
    public async Task Approve_ByCreator_IsRejected()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await CreateWorkerAsync(harness, "W-003");
        await CreateContractAsync(harness, worker.Id);
        var importId = await ImportTimesheetAsync(harness, "W-003", 8m);

        harness.User.UserId = "admin.user";

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [importId], null));

        await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(created.Value.Id));
        await harness.SubmitHandler.HandleAsync(new SubmitPayrollRunCommand(created.Value.Id));

        var decision = await harness.DecideHandler.HandleAsync(
            new DecidePayrollRunCommand(created.Value.Id, Approve: true, Reason: null));

        Assert.True(decision.IsFailure);
        Assert.Equal("payroll.maker_checker_violation", decision.Error.Code);
    }

    [Fact]
    public async Task Decide_WithoutApproverRole_IsForbidden()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await CreateWorkerAsync(harness, "W-004");
        await CreateContractAsync(harness, worker.Id);
        var importId = await ImportTimesheetAsync(harness, "W-004", 8m);

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [importId], null));

        await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(created.Value.Id));
        await harness.SubmitHandler.HandleAsync(new SubmitPayrollRunCommand(created.Value.Id));

        harness.Access.CanApprovePayroll = false;

        var decision = await harness.DecideHandler.HandleAsync(
            new DecidePayrollRunCommand(created.Value.Id, Approve: true, Reason: null));

        Assert.True(decision.IsFailure);
        Assert.Equal("auth.forbidden", decision.Error.Code);
    }

    [Fact]
    public async Task CreateRun_WhenImportNotCompleted_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var csv = Encoding.UTF8.GetBytes("workerRef,workDate,units\nW-005,2026-09-10,8\n");
        var sha = Convert.ToHexString(SHA256.HashData(csv)).ToLowerInvariant();

        var registration = await harness.RegisterImportHandler.HandleAsync(
            new RegisterTimesheetImportCommand(PeriodStart, PeriodEnd, "timesheets.csv", sha, csv.Length));

        Assert.True(registration.IsSuccess);

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [registration.Value.ImportId], null));

        Assert.True(created.IsFailure);
        Assert.Equal("payroll.import_not_completed", created.Error.Code);
    }

    [Fact]
    public async Task CreateRun_WithoutTenant_IsUnauthorized()
    {
        var harness = new PayrollHarness(null);

        var created = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [Guid.NewGuid()], null));

        Assert.True(created.IsFailure);
        Assert.Equal("tenant.required", created.Error.Code);
    }

    private static async Task<WorkerResponse> CreateWorkerAsync(PayrollHarness harness, string externalRef)
    {
        var result = await harness.CreateWorkerHandler.HandleAsync(
            new CreateWorkerCommand(externalRef, $"Worker {externalRef}", null));

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    private static async Task CreateContractAsync(PayrollHarness harness, Guid workerId)
    {
        var result = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(
                workerId,
                new DateOnly(2026, 1, 1),
                null,
                "USD",
                [
                    new CompensationComponentRequest("Earning", "BASE", "HourlyRate", 25m),
                    new CompensationComponentRequest("Deduction", "TAX", "PercentOfGross", 10m),
                ]));

        Assert.True(result.IsSuccess);
    }

    private static async Task<Guid> ImportTimesheetAsync(PayrollHarness harness, string workerRef, decimal units)
    {
        var csv = Encoding.UTF8.GetBytes($"workerRef,workDate,units\n{workerRef},2026-09-15,{units}\n");
        var sha = Convert.ToHexString(SHA256.HashData(csv)).ToLowerInvariant();

        var registration = await harness.RegisterImportHandler.HandleAsync(
            new RegisterTimesheetImportCommand(PeriodStart, PeriodEnd, "timesheets.csv", sha, csv.Length));

        Assert.True(registration.IsSuccess);

        harness.Blobs.Seed(
            GlobalPay.Application.Modules.TimeImport.ImportBlobNames.Container,
            registration.Value.BlobName,
            csv);

        var processed = await harness.ProcessImportHandler.HandleAsync(
            new ProcessTimesheetImportCommand(registration.Value.ImportId, registration.Value.BlobName));

        Assert.True(processed.IsSuccess);
        Assert.Equal(1, processed.Value.ValidRows);

        return registration.Value.ImportId;
    }
}
