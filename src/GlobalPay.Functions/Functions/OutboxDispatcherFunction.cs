using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Payments;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GlobalPay.Functions.Functions;

public sealed class OutboxDispatcherFunction
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherFunction> _logger;

    public OutboxDispatcherFunction(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherFunction> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Function("OutboxDispatcher")]
    public async Task RunAsync(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<DispatchOutboxCommand, OutboxDispatchResult>>();

        var result = await handler
            .HandleAsync(new DispatchOutboxCommand(BatchSize: 20, LeaseDuration), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning("Outbox dispatch failed with {Code}", result.Error.Code);

            return;
        }

        if (result.Value.Leased > 0)
        {
            _logger.LogInformation(
                "Outbox dispatch leased {Leased}, processed {Processed}, failed {Failed}",
                result.Value.Leased,
                result.Value.Processed,
                result.Value.Failed);
        }
    }
}
