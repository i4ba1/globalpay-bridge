using GlobalPay.Application.Abstractions;

namespace GlobalPay.Api.Middleware;

internal sealed class TenantResolutionMiddleware : IMiddleware
{
    public const string TenantClaim = "tenant_id";

    private readonly ITenantContextMutator _tenantContextMutator;

    public TenantResolutionMiddleware(ITenantContextMutator tenantContextMutator) =>
        _tenantContextMutator = tenantContextMutator;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var claimValue = context.User.FindFirst(TenantClaim)?.Value;

        if (Guid.TryParse(claimValue, out var tenantId))
        {
            _tenantContextMutator.SetTenant(tenantId);
        }

        return next(context);
    }
}
