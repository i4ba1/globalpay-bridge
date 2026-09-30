using System.Data.Common;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Infrastructure.Persistence;
using GlobalPay.Infrastructure.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Messaging;

internal sealed class InboxStore : IInboxStore
{
    private readonly GlobalPayDbContext _context;

    public InboxStore(GlobalPayDbContext context) => _context = context;

    public async Task<bool> TryAddAsync(
        string providerCode,
        string providerEventId,
        string payloadHash,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var message = new InboxMessage(Guid.NewGuid(), providerCode, providerEventId, payloadHash, receivedAtUtc);

        await _context.InboxMessages.AddAsync(message, cancellationToken).ConfigureAwait(false);

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is DbException)
        {
            _context.Entry(message).State = EntityState.Detached;

            return false;
        }
    }

    public async Task MarkProcessedAsync(
        string providerCode,
        string providerEventId,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.InboxMessages
            .FirstOrDefaultAsync(
                candidate => candidate.ProviderCode == providerCode && candidate.ProviderEventId == providerEventId,
                cancellationToken)
            .ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        message.MarkProcessed(processedAtUtc);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
