using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Modules.Workforce.Commands;

public sealed record CreateWorkerCommand(
    string ExternalRef,
    string DisplayName,
    string? Email) : ICommand<WorkerResponse>;

internal sealed class CreateWorkerCommandHandler : ICommandHandler<CreateWorkerCommand, WorkerResponse>
{
    private readonly IWorkerRepository _workers;
    private readonly ITenantContext _tenantContext;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateWorkerCommandHandler(
        IWorkerRepository workers,
        ITenantContext tenantContext,
        IAccessPolicy accessPolicy,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _workers = workers;
        _tenantContext = tenantContext;
        _accessPolicy = accessPolicy;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<WorkerResponse>> HandleAsync(
        CreateWorkerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<WorkerResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanManageWorkforce)
        {
            return Result.Failure<WorkerResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to manage workers."));
        }

        if (await _workers.ExternalRefExistsAsync(command.ExternalRef, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<WorkerResponse>(
                Error.Conflict("worker.external_ref_exists", $"Worker reference '{command.ExternalRef}' already exists."));
        }

        var worker = Worker.Create(
            _tenantContext.TenantId!.Value,
            command.ExternalRef,
            command.DisplayName,
            command.Email,
            _timeProvider.GetUtcNow());

        await _workers.AddAsync(worker, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkforceMapper.ToResponse(worker);
    }
}
