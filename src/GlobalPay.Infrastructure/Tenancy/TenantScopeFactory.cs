using GlobalPay.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalPay.Infrastructure.Tenancy;

internal sealed class TenantScopeFactory : ITenantScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantScopeFactory(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<TResult> ExecuteAsync<TResult>(
        Guid? tenantId,
        Func<IServiceProvider, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _scopeFactory.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<ITenantContextMutator>().SetTenant(tenantId);

        return await operation(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }
}
