using System.Security.Cryptography;
using System.Text;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Application.Modules.Workforce.Commands;
using GlobalPay.Application.Modules.Workforce.Queries;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Modules.TimeImport;
using GlobalPay.UnitTests.Support;

namespace GlobalPay.UnitTests;

public sealed class WorkforceHandlerTests
{
    [Fact]
    public async Task CreateWorker_WhenExternalRefExists_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-100", "First", null));

        var duplicate = await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-100", "Second", null));

        Assert.True(duplicate.IsFailure);
        Assert.Equal("worker.external_ref_exists", duplicate.Error.Code);
    }

    [Fact]
    public async Task CreateWorker_WithoutManagePermission_IsForbidden()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        harness.Access.CanManageWorkforce = false;

        var result = await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-101", "Worker", null));

        Assert.True(result.IsFailure);
        Assert.Equal("auth.forbidden", result.Error.Code);
    }

    [Fact]
    public async Task GetWorker_WhenMissing_IsNotFound()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var result = await harness.GetWorkerHandler.HandleAsync(new GetWorkerQuery(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("worker.not_found", result.Error.Code);
    }

    [Fact]
    public async Task CreateContract_WhenOverlapping_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-102", "Worker", null));

        var first = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(worker.Value.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "USD", [Component()]));

        Assert.True(first.IsSuccess);

        var overlapping = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(worker.Value.Id, new DateOnly(2026, 6, 1), null, "USD", [Component()]));

        Assert.True(overlapping.IsFailure);
        Assert.Equal("contract.overlap", overlapping.Error.Code);
    }

    [Fact]
    public async Task CreateContract_WhenWorkerMissing_IsNotFound()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var result = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(Guid.NewGuid(), new DateOnly(2026, 1, 1), null, "USD", [Component()]));

        Assert.True(result.IsFailure);
        Assert.Equal("worker.not_found", result.Error.Code);
    }

    [Fact]
    public async Task CreateContract_WithDuplicateComponentCode_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-103", "Worker", null));

        var result = await harness.CreateContractHandler.HandleAsync(
            new CreateContractCommand(
                worker.Value.Id,
                new DateOnly(2026, 1, 1),
                null,
                "USD",
                [Component(), Component()]));

        Assert.True(result.IsFailure);
        Assert.Equal("workforce.component_duplicate", result.Error.Code);
    }

    private static CompensationComponentRequest Component() =>
        new("Earning", "BASE", "HourlyRate", 20m);
}

public sealed class TimesheetImportHandlerTests
{
    private static readonly DateOnly PeriodStart = new(2026, 9, 1);

    private static readonly DateOnly PeriodEnd = new(2026, 9, 30);

    [Fact]
    public async Task ProcessImport_ImportsValidRowsAndReportsUnknownWorkers()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-200", "Known", null));

        var csv = Encoding.UTF8.GetBytes(
            "workerRef,workDate,units\nW-200,2026-09-10,8\nW-999,2026-09-11,4\n");

        var (importId, blobName) = await RegisterAsync(harness, csv);

        var result = await harness.ProcessImportHandler.HandleAsync(
            new ProcessTimesheetImportCommand(importId, blobName));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalRows);
        Assert.Equal(1, result.Value.ValidRows);
        Assert.Equal(1, result.Value.InvalidRows);
        Assert.Contains(result.Value.Errors, error => error.Code == "import.unknown_worker");
        Assert.Single(harness.TimeEntries.All);
    }

    [Fact]
    public async Task ProcessImport_WhenChecksumDoesNotMatch_Fails()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var registered = Encoding.UTF8.GetBytes("workerRef,workDate,units\nW-300,2026-09-10,8\n");
        var (importId, blobName) = await RegisterAsync(harness, registered);

        harness.Blobs.Seed(
            GlobalPay.Application.Modules.TimeImport.ImportBlobNames.Container,
            blobName,
            Encoding.UTF8.GetBytes("workerRef,workDate,units\nW-300,2026-09-10,9\n"));

        var result = await harness.ProcessImportHandler.HandleAsync(
            new ProcessTimesheetImportCommand(importId, blobName));

        Assert.True(result.IsFailure);

        var import = await harness.Imports.GetByIdAsync(importId);
        Assert.NotNull(import);
        Assert.Equal(ImportStatus.Failed, import.Status);
    }

    [Fact]
    public async Task ProcessImport_IsIdempotent_ForCompletedImport()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        await harness.CreateWorkerHandler.HandleAsync(new CreateWorkerCommand("W-400", "Known", null));

        var csv = Encoding.UTF8.GetBytes("workerRef,workDate,units\nW-400,2026-09-10,8\n");
        var (importId, blobName) = await RegisterAsync(harness, csv);

        var first = await harness.ProcessImportHandler.HandleAsync(
            new ProcessTimesheetImportCommand(importId, blobName));
        var second = await harness.ProcessImportHandler.HandleAsync(
            new ProcessTimesheetImportCommand(importId, blobName));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(ImportStatus.Completed.ToString(), second.Value.Status);
        Assert.Single(harness.TimeEntries.All);
    }

    private static async Task<(Guid ImportId, string BlobName)> RegisterAsync(PayrollHarness harness, byte[] content)
    {
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var registration = await harness.RegisterImportHandler.HandleAsync(
            new RegisterTimesheetImportCommand(PeriodStart, PeriodEnd, "timesheets.csv", sha, content.Length));

        Assert.True(registration.IsSuccess);

        harness.Blobs.Seed(
            GlobalPay.Application.Modules.TimeImport.ImportBlobNames.Container,
            registration.Value.BlobName,
            content);

        return (registration.Value.ImportId, registration.Value.BlobName);
    }
}
