using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Infrastructure.Persistence;
using GlobalPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Messaging;

internal sealed class OutboxWriter : IOutboxWriter
{
    private readonly GlobalPayDbContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxWriter(GlobalPayDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task EnqueueAsync(string type, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var now = _timeProvider.GetUtcNow();

        await _context.OutboxMessages
            .AddAsync(OutboxMessage.Create(type, payload, now, now), cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class OutboxStore : IOutboxStore
{
    private readonly GlobalPayDbContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxStore(GlobalPayDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<OutboxEnvelope>> LeaseAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var leaseUntil = now.Add(leaseDuration);

        var candidates = await _context.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null
                              && message.AvailableAtUtc <= now
                              && (message.LeaseUntilUtc == null || message.LeaseUntilUtc < now))
            .OrderBy(message => message.AvailableAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in candidates)
        {
            message.Lease(leaseUntil);
        }

        if (candidates.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return candidates
            .Select(message => new OutboxEnvelope(message.Id, message.Type, message.Payload, message.Attempts))
            .ToArray();
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        message.MarkProcessed(_timeProvider.GetUtcNow());

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid id,
        string error,
        TimeSpan nextAttemptDelay,
        CancellationToken cancellationToken = default)
    {
        var message = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        message.RecordAttempt(error, _timeProvider.GetUtcNow().Add(nextAttemptDelay));

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<OutboxMessage?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        _context.OutboxMessages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);
}
