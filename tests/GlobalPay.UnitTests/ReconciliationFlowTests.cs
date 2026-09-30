using System.Text.Json;
using GlobalPay.Application.Modules.Payments;
using GlobalPay.Application.Modules.Reconciliation;
using GlobalPay.Contracts.Payments;
using GlobalPay.Domain.Modules.Reconciliation;
using GlobalPay.UnitTests.Support;

namespace GlobalPay.UnitTests;

public sealed class ReconciliationFlowTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task Reconcile_OpensCaseForUnconfirmedInstruction()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        var result = await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Scanned);
        Assert.Equal(1, result.Value.Opened);

        var reconciliationCase = Assert.Single(harness.ReconciliationCases.All);
        Assert.Equal(ReconciliationReason.MissingConfirmation, reconciliationCase.Reason);
        Assert.Equal(ReconciliationCaseStatus.Open, reconciliationCase.Status);
    }

    [Fact]
    public async Task Reconcile_IsIdempotent_ForExistingCases()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));
        var second = await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));

        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value.Opened);
        Assert.Equal(1, second.Value.Existing);
        Assert.Single(harness.ReconciliationCases.All);
    }

    [Fact]
    public async Task Reconcile_SettledBatch_OpensNoCase()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        await harness.WebhookHandler.HandleAsync(new ProcessPaymentWebhookCommand(
            "evt-1",
            1_700_000_000,
            "signature",
            BuildPayload("payment.settled", "prov-ref-1")));

        var result = await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Scanned);
        Assert.Empty(harness.ReconciliationCases.All);
    }

    [Fact]
    public async Task ResolveCase_RecordsOwnerAndNote()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));
        await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));

        var reconciliationCase = Assert.Single(harness.ReconciliationCases.All);

        harness.User.UserId = "finance.user";

        var resolved = await harness.ResolveCaseHandler.HandleAsync(
            new ResolveReconciliationCaseCommand(reconciliationCase.Id, WriteOff: false, Note: "Confirmed with provider"));

        Assert.True(resolved.IsSuccess);
        Assert.Equal(ReconciliationCaseStatus.Resolved.ToString(), resolved.Value.Status);
        Assert.Equal("finance.user", resolved.Value.Owner);
        Assert.Equal("Confirmed with provider", resolved.Value.ResolutionNote);
    }

    [Fact]
    public async Task ResolveCase_Twice_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));
        await harness.ReconcileHandler.HandleAsync(new ReconcilePaymentsCommand(100));

        var reconciliationCase = Assert.Single(harness.ReconciliationCases.All);

        await harness.ResolveCaseHandler.HandleAsync(
            new ResolveReconciliationCaseCommand(reconciliationCase.Id, false, "First"));

        var second = await harness.ResolveCaseHandler.HandleAsync(
            new ResolveReconciliationCaseCommand(reconciliationCase.Id, false, "Second"));

        Assert.True(second.IsFailure);
        Assert.Equal("reconciliation.case_closed", second.Error.Code);
    }

    [Fact]
    public async Task GetCases_RequiresFinanceOrAuditor()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        harness.Access.CanPay = false;
        harness.Access.CanAudit = false;

        var result = await harness.GetCasesHandler.HandleAsync(
            new GetReconciliationCasesQuery(null, null, null, null, 50));

        Assert.True(result.IsFailure);
        Assert.Equal("auth.forbidden", result.Error.Code);
    }

    private static string BuildPayload(string eventType, string reference) =>
        JsonSerializer.Serialize(
            new PaymentProviderWebhookPayload(
                eventType,
                reference,
                200m,
                "USD",
                new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero)),
            SerializerOptions);
}
