namespace GlobalPay.Application.Abstractions;

public interface ITenantScopeFactory
{
    Task<TResult> ExecuteAsync<TResult>(
        Guid? tenantId,
        Func<IServiceProvider, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
