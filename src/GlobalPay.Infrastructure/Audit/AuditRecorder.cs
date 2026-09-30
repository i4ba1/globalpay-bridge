using System.Diagnostics;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Modules.Audit;

namespace GlobalPay.Infrastructure.Audit;

internal sealed class AuditRecorder : IAuditRecorder
{
    private readonly IAuditEventRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public AuditRecorder(
        IAuditEventRepository repository,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(
        string action,
        string targetType,
        Guid targetId,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return;
        }

        var auditEvent = AuditEvent.Record(
            _tenantContext.TenantId!.Value,
            _currentUser.UserId ?? "system",
            action,
            targetType,
            targetId,
            _timeProvider.GetUtcNow(),
            Activity.Current?.TraceId.ToString(),
            data);

        await _repository.AddAsync(auditEvent, cancellationToken).ConfigureAwait(false);
    }
}
