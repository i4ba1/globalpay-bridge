using System.Text.Json;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Payments;
using GlobalPay.Application.Modules.Payments;
using GlobalPay.Contracts.Payments;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Payments;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.UnitTests.Support;

namespace GlobalPay.UnitTests;

public sealed class PaymentsFlowTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task CreateBatch_RequiresAnApprovedRun()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var worker = await harness.CreateWorkerHandler.HandleAsync(
            new GlobalPay.Application.Modules.Workforce.Commands.CreateWorkerCommand("W-900", "Worker", null));

        var run = await harness.CreateRunHandler.HandleAsync(
            new GlobalPay.Application.Modules.Payroll.Commands.CreatePayrollRunCommand(
                Scenario.PeriodStart, Scenario.PeriodEnd, "USD", [Guid.NewGuid()], null));

        Assert.True(worker.IsSuccess);

        var batch = await harness.CreateBatchHandler.HandleAsync(
            new CreatePaymentBatchCommand(run.IsSuccess ? run.Value.Id : Guid.NewGuid(), "key-1"));

        Assert.True(batch.IsFailure);
    }

    [Fact]
    public async Task CreateBatch_IsIdempotent_ForTheSameRunAndKey()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        var first = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        var retry = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value.Id, retry.Value.Id);
        Assert.Single(harness.PaymentBatches.All);
    }

    [Fact]
    public async Task CreateBatch_WithSameKeyForDifferentRun_IsConflict()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var firstRunId = await Scenario.BuildApprovedRunAsync(harness, "W-910", 25m, 8m);
        var secondRunId = await Scenario.BuildApprovedRunAsync(harness, "W-911", 30m, 4m);

        var first = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(firstRunId, "shared-key"));
        var second = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(secondRunId, "shared-key"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("idempotency.fingerprint_mismatch", second.Error.Code);
    }

    [Fact]
    public async Task Dispatch_SubmitsBatchToProvider_AndMarksItSubmitted()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        var created = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        Assert.True(created.IsSuccess);
        Assert.Equal(PaymentBatchStatus.Created.ToString(), created.Value.Status);

        var dispatched = await harness.DispatchHandler.HandleAsync(
            new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        Assert.True(dispatched.IsSuccess);
        Assert.Equal(1, dispatched.Value.Processed);
        Assert.Equal(0, dispatched.Value.Failed);

        var submission = Assert.Single(harness.PaymentProvider.Submissions);
        Assert.Equal(created.Value.Id, submission.PaymentBatchId);

        var reloaded = await harness.GetBatchHandler.HandleAsync(new GetPaymentBatchQuery(created.Value.Id));

        Assert.True(reloaded.IsSuccess);
        Assert.Equal(PaymentBatchStatus.Submitted.ToString(), reloaded.Value.Status);
        Assert.Equal("prov-ref-1", reloaded.Value.ProviderReference);
    }

    [Fact]
    public async Task Dispatch_TransientFailure_KeepsMessageForRetry()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));

        harness.PaymentProvider.NextResult = new PaymentSubmissionResult(false, null, "provider.http_503", "unavailable");

        var dispatched = await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        Assert.True(dispatched.IsSuccess);
        Assert.Equal(1, dispatched.Value.Failed);
        Assert.Single(harness.Outbox.PendingIds);
        Assert.Equal(1, harness.Outbox.AttemptsOf(harness.Outbox.PendingIds.First()));
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_IsRejected()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        harness.SignatureVerifier.IsValid = false;

        var result = await harness.WebhookHandler.HandleAsync(
            new ProcessPaymentWebhookCommand("evt-1", 1_700_000_000, "bad", "{}"));

        Assert.True(result.IsFailure);
        Assert.Equal("webhook.signature_invalid", result.Error.Code);
    }

    [Fact]
    public async Task Webhook_SettlementMarksBatchSettled_AndRunPaid()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        var created = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        var settled = await SendEventAsync(harness, "evt-1", PaymentProviderEventTypes.Settled, "prov-ref-1");

        Assert.True(settled.IsSuccess);

        var batch = await harness.GetBatchHandler.HandleAsync(new GetPaymentBatchQuery(created.Value.Id));

        Assert.True(batch.IsSuccess);
        Assert.Equal(PaymentBatchStatus.Settled.ToString(), batch.Value.Status);

        var run = await harness.Runs.GetByIdAsync(runId);

        Assert.NotNull(run);
        Assert.Equal(PayrollRunStatus.Paid, run.Status);
    }

    [Fact]
    public async Task Webhook_DuplicateEvent_IsAcknowledgedWithoutMutation()
    {
        var harness = new PayrollHarness(Guid.NewGuid());
        var runId = await Scenario.BuildApprovedRunAsync(harness);

        await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
        await harness.DispatchHandler.HandleAsync(new DispatchOutboxCommand(20, TimeSpan.FromMinutes(2)));

        var first = await SendEventAsync(harness, "evt-dup", PaymentProviderEventTypes.Accepted, "prov-ref-1");
        var second = await SendEventAsync(harness, "evt-dup", PaymentProviderEventTypes.Accepted, "prov-ref-1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var acceptedCount = harness.Audit.Records.Count(record => record.Action == "payments.webhook.applied");

        Assert.Equal(1, acceptedCount);
    }

    [Fact]
    public async Task Webhook_UnknownReference_IsNotFound()
    {
        var harness = new PayrollHarness(Guid.NewGuid());

        var result = await SendEventAsync(harness, "evt-x", PaymentProviderEventTypes.Accepted, "does-not-exist");

        Assert.True(result.IsFailure);
        Assert.Equal("payments.batch_not_found", result.Error.Code);
    }

    private static async Task<Result<Unit>> SendEventAsync(
        PayrollHarness harness,
        string eventId,
        string eventType,
        string paymentReference)
    {
        var payload = JsonSerializer.Serialize(
            new PaymentProviderWebhookPayload(eventType, paymentReference, 200m, "USD", harness.Clock.UtcNow),
            SerializerOptions);

        return await harness.WebhookHandler.HandleAsync(
            new ProcessPaymentWebhookCommand(eventId, 1_700_000_000, "signature", payload));
    }
}
