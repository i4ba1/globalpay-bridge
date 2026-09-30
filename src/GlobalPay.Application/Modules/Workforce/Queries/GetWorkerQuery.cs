using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Modules.Workforce.Queries;

public sealed record GetWorkerQuery(Guid WorkerId) : IQuery<WorkerResponse>;

internal sealed class GetWorkerQueryHandler : IQueryHandler<GetWorkerQuery, WorkerResponse>
{
    private readonly IWorkerRepository _workers;
    private readonly ITenantContext _tenantContext;

    public GetWorkerQueryHandler(IWorkerRepository workers, ITenantContext tenantContext)
    {
        _workers = workers;
        _tenantContext = tenantContext;
    }

    public async Task<Result<WorkerResponse>> HandleAsync(
        GetWorkerQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<WorkerResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        var worker = await _workers.GetByIdAsync(query.WorkerId, cancellationToken).ConfigureAwait(false);

        if (worker is null)
        {
            return Result.Failure<WorkerResponse>(
                Error.NotFound("worker.not_found", $"Worker '{query.WorkerId}' was not found."));
        }

        return WorkforceMapper.ToResponse(worker);
    }
}
