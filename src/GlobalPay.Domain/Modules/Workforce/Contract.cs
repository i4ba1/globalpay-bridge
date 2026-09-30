using GlobalPay.Domain.Common;
using GlobalPay.Domain.Finance;

namespace GlobalPay.Domain.Modules.Workforce;

public sealed class Contract : AggregateRoot<Guid>, ITenantScoped
{
    private readonly List<CompensationComponent> _components = [];

    private Contract()
    {
    }

    private Contract(
        Guid id,
        Guid tenantId,
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        Currency currency,
        DateTimeOffset createdAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        WorkerId = workerId;
        StartDate = startDate;
        EndDate = endDate;
        Currency = currency;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TenantId { get; private set; }

    public Guid WorkerId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<CompensationComponent> Components => _components.AsReadOnly();

    public static Contract Create(
        Guid tenantId,
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        Currency currency,
        DateTimeOffset createdAtUtc)
    {
        Guard.NotNull(currency, nameof(currency));

        if (endDate is not null && endDate < startDate)
        {
            throw new DomainException(
                "Contract end date must not be before the start date.",
                "workforce.contract_dates_invalid");
        }

        return new Contract(
            Guid.NewGuid(),
            Guard.NotEmpty(tenantId, nameof(tenantId)),
            Guard.NotEmpty(workerId, nameof(workerId)),
            startDate,
            endDate,
            currency,
            createdAtUtc);
    }

    public CompensationComponent AddComponent(
        CompensationType type,
        string code,
        CalculationRule rule,
        decimal amount)
    {
        if (_components.Exists(component => string.Equals(component.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException(
                $"Component code '{code}' already exists on this contract.",
                "workforce.component_duplicate");
        }

        var component = CompensationComponent.Create(TenantId, Id, type, code, rule, amount);
        _components.Add(component);

        return component;
    }

    public bool IsEffectiveOn(DateOnly date) =>
        date >= StartDate && (EndDate is null || date <= EndDate);

    public static bool RangesOverlap(DateOnly firstStart, DateOnly? firstEnd, DateOnly secondStart, DateOnly? secondEnd)
    {
        var firstEffectiveEnd = firstEnd ?? DateOnly.MaxValue;
        var secondEffectiveEnd = secondEnd ?? DateOnly.MaxValue;

        return firstStart <= secondEffectiveEnd && secondStart <= firstEffectiveEnd;
    }
}
