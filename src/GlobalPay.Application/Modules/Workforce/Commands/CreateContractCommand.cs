using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Modules.Workforce.Commands;

public sealed record CreateContractCommand(
    Guid WorkerId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Currency,
    IReadOnlyList<CompensationComponentRequest> Components) : ICommand<ContractResponse>;

internal sealed class CreateContractCommandHandler : ICommandHandler<CreateContractCommand, ContractResponse>
{
    private readonly IWorkerRepository _workers;
    private readonly IContractRepository _contracts;
    private readonly ITenantContext _tenantContext;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateContractCommandHandler(
        IWorkerRepository workers,
        IContractRepository contracts,
        ITenantContext tenantContext,
        IAccessPolicy accessPolicy,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _workers = workers;
        _contracts = contracts;
        _tenantContext = tenantContext;
        _accessPolicy = accessPolicy;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ContractResponse>> HandleAsync(
        CreateContractCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<ContractResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanManageWorkforce)
        {
            return Result.Failure<ContractResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to manage contracts."));
        }

        var worker = await _workers.GetByIdAsync(command.WorkerId, cancellationToken).ConfigureAwait(false);

        if (worker is null)
        {
            return Result.Failure<ContractResponse>(
                Error.NotFound("worker.not_found", $"Worker '{command.WorkerId}' was not found."));
        }

        if (await _contracts
                .HasOverlappingContractAsync(command.WorkerId, command.StartDate, command.EndDate, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<ContractResponse>(
                Error.Conflict(
                    "contract.overlap",
                    "An effective-dated contract already covers part of the requested period."));
        }

        var currency = Currency.FromCode(command.Currency);

        var contract = Contract.Create(
            _tenantContext.TenantId!.Value,
            worker.Id,
            command.StartDate,
            command.EndDate,
            currency,
            _timeProvider.GetUtcNow());

        foreach (var component in command.Components)
        {
            try
            {
                contract.AddComponent(
                    Enum.Parse<CompensationType>(component.Type, ignoreCase: true),
                    component.Code,
                    Enum.Parse<CalculationRule>(component.Rule, ignoreCase: true),
                    component.Amount);
            }
            catch (DomainException exception)
            {
                return Result.Failure<ContractResponse>(exception.ToError("contract.component_invalid"));
            }
        }

        await _contracts.AddAsync(contract, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkforceMapper.ToResponse(contract);
    }
}
