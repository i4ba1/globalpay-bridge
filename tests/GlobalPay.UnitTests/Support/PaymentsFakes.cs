using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Payments;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Payments;

namespace GlobalPay.UnitTests.Support;

internal sealed class InMemoryPaymentBatchRepository : IPaymentBatchRepository
{
    private readonly List<PaymentBatch> _batches = [];

    public IReadOnlyList<PaymentBatch> All => _batches;

    public Task AddAsync(PaymentBatch batch, CancellationToken cancellationToken = default)
    {
        _batches.Add(batch);

        return Task.CompletedTask;
    }

    public Task<PaymentBatch?> GetByIdAsync(Guid paymentBatchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_batches.FirstOrDefault(batch => batch.Id == paymentBatchId));

    public Task<PaymentBatch?> GetByPayrollRunAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_batches.FirstOrDefault(batch => batch.PayrollRunId == payrollRunId));

    public Task<PaymentBatch?> GetByProviderReferenceAsync(
        string providerReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_batches.FirstOrDefault(batch =>
            string.Equals(batch.ProviderReference, providerReference, StringComparison.Ordinal)));

    public Task<IReadOnlyList<PaymentBatch>> ListOpenAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentBatch>>(_batches
            .Where(batch => batch.Status is PaymentBatchStatus.Submitted or PaymentBatchStatus.PartiallySettled)
            .Take(limit)
            .ToArray());
}

internal sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly Dictionary<Guid, OutboxEntry> _messages = [];

    public IReadOnlyCollection<Guid> PendingIds =>
        _messages.Where(pair => !pair.Value.Processed).Select(pair => pair.Key).ToArray();

    public IReadOnlyCollection<Guid> ProcessedIds =>
        _messages.Where(pair => pair.Value.Processed).Select(pair => pair.Key).ToArray();

    public int AttemptsOf(Guid id) => _messages.TryGetValue(id, out var entry) ? entry.Attempts : 0;

    public void Seed(Guid id, string type, string payload) =>
        _messages[id] = new OutboxEntry(type, payload, 0, false);

    public Task<IReadOnlyList<OutboxEnvelope>> LeaseAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var leased = _messages
            .Where(pair => !pair.Value.Processed)
            .Take(batchSize)
            .Select(pair => new OutboxEnvelope(pair.Key, pair.Value.Type, pair.Value.Payload, pair.Value.Attempts))
            .ToArray();

        return Task.FromResult<IReadOnlyList<OutboxEnvelope>>(leased);
    }

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(id, out var entry))
        {
            _messages[id] = entry with { Processed = true };
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan nextAttemptDelay,
        CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(id, out var entry))
        {
            _messages[id] = entry with { Attempts = entry.Attempts + 1, LastError = error };
        }

        return Task.CompletedTask;
    }

    private sealed record OutboxEntry(string Type, string Payload, int Attempts, bool Processed, string? LastError = null);
}

internal sealed class InMemoryOutboxWriter : IOutboxWriter
{
    private readonly InMemoryOutboxStore _store;

    public InMemoryOutboxWriter(InMemoryOutboxStore store) => _store = store;

    public Task EnqueueAsync(string type, string payload, CancellationToken cancellationToken = default)
    {
        _store.Seed(Guid.NewGuid(), type, payload);

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryInboxStore : IInboxStore
{
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public Task<bool> TryAddAsync(
        string providerCode,
        string providerEventId,
        string payloadHash,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_keys.Add($"{providerCode}:{providerEventId}"));

    public Task MarkProcessedAsync(
        string providerCode,
        string providerEventId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class FakePaymentProvider : IPaymentProvider
{
    public PaymentSubmissionResult NextResult { get; set; } = new(true, "prov-ref-1", null, null);

    public List<PaymentSubmission> Submissions { get; } = [];

    public Task<PaymentSubmissionResult> SubmitAsync(
        PaymentSubmission submission,
        CancellationToken cancellationToken = default)
    {
        Submissions.Add(submission);

        return Task.FromResult(NextResult);
    }

    public Task<PaymentStatusResult> GetStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentStatusResult("settled", providerReference, null));
}

internal sealed class TestSignatureVerifier : IWebhookSignatureVerifier
{
    public bool IsValid { get; set; } = true;

    public bool Verify(string signature, long timestamp, ReadOnlySpan<byte> rawBody) => IsValid;
}
