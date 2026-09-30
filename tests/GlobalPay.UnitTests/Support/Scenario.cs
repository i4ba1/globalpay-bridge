using System.Security.Cryptography;
using System.Text;
using GlobalPay.Application.Modules.Payroll.Commands;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Application.Modules.Workforce.Commands;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Modules.Payroll;

namespace GlobalPay.UnitTests.Support;

internal static class Scenario
{
    public static readonly DateOnly PeriodStart = new(2026, 9, 1);

    public static readonly DateOnly PeriodEnd = new(2026, 9, 30);

    public static async Task<Guid> BuildApprovedRunAsync(
        PayrollHarness harness,
        string workerRef = "W-001",
        decimal hourlyRate = 25m,
        decimal units = 8m)
    {
        ArgumentNullException.ThrowIfNull(harness);

        harness.User.UserId = "admin.user";

        var worker = await harness.CreateWorkerHandler.HandleAsync(
            new CreateWorkerCommand(workerRef, $"Worker {workerRef}", null));

        Assert.True(worker.IsSuccess);

        var contract = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(
                worker.Value.Id,
                new DateOnly(2026, 1, 1),
                null,
                "USD",
                [new CompensationComponentRequest("Earning", "BASE", "HourlyRate", hourlyRate)]));

        Assert.True(contract.IsSuccess);

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

        var run = await harness.CreateRunHandler.HandleAsync(
            new CreatePayrollRunCommand(PeriodStart, PeriodEnd, "USD", [registration.Value.ImportId], null));

        Assert.True(run.IsSuccess);

        var calculated = await harness.CalculateHandler.HandleAsync(new CalculatePayrollRunCommand(run.Value.Id));

        Assert.True(calculated.IsSuccess);

        var submitted = await harness.SubmitHandler.HandleAsync(new SubmitPayrollRunCommand(run.Value.Id));

        Assert.True(submitted.IsSuccess);

        harness.User.UserId = "approver.user";

        var approved = await harness.DecideHandler.HandleAsync(new DecidePayrollRunCommand(run.Value.Id, true, null));

        Assert.True(approved.IsSuccess);
        Assert.Equal(PayrollRunStatus.Approved.ToString(), approved.Value.Status);

        harness.User.UserId = "finance.user";

        return run.Value.Id;
    }
}
