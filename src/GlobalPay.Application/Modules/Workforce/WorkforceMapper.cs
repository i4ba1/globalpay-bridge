using GlobalPay.Contracts.Workers;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Application.Modules.Workforce;

internal static class WorkforceMapper
{
    public static WorkerResponse ToResponse(Worker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);

        return new WorkerResponse(
            worker.Id,
            worker.ExternalRef,
            worker.DisplayName,
            worker.Email,
            worker.Status.ToString(),
            worker.CreatedAtUtc);
    }

    public static ContractResponse ToResponse(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return new ContractResponse(
            contract.Id,
            contract.WorkerId,
            contract.StartDate,
            contract.EndDate,
            contract.Currency.Code,
            contract.Components
                .Select(component => new CompensationComponentResponse(
                    component.Id,
                    component.Type.ToString(),
                    component.Code,
                    component.Rule.ToString(),
                    component.Amount))
                .ToArray(),
            contract.CreatedAtUtc);
    }
}
