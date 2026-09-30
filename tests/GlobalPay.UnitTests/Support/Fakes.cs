using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Idempotency;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Application.Abstractions.Storage;
using GlobalPay.Domain.Modules.Audit;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Domain.Modules.TimeImport;
using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.UnitTests.Support;

internal sealed class TestTenantContext : ITenantContext, ITenantContextMutator
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;

    public void SetTenant(Guid? tenantId) => TenantId = tenantId == Guid.Empty ? null : tenantId;
}

internal sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; } = true;

    public string? UserId { get; set; } = "test.user";

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.Ordinal);
}

internal sealed class TestAccessPolicy : IAccessPolicy
{
    public bool CanManageWorkforce { get; set; } = true;

    public bool CanManagePayroll { get; set; } = true;

    public bool CanApprovePayroll { get; set; } = true;

    public bool CanPay { get; set; } = true;

    public bool CanAudit { get; set; } = true;
}

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        return Task.FromResult(1);
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        operation(cancellationToken);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}

internal sealed class RecordingAuditRecorder : IAuditRecorder
{
    public List<(string Action, string TargetType, Guid TargetId)> Records { get; } = [];

    public Task RecordAsync(
        string action,
        string targetType,
        Guid targetId,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        Records.Add((action, targetType, targetId));

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryWorkerRepository : IWorkerRepository
{
    private readonly List<Worker> _workers = [];

    public IReadOnlyList<Worker> All => _workers;

    public Task AddAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        _workers.Add(worker);

        return Task.CompletedTask;
    }

    public Task<Worker?> GetByIdAsync(Guid workerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_workers.FirstOrDefault(worker => worker.Id == workerId));

    public Task<bool> ExternalRefExistsAsync(string externalRef, CancellationToken cancellationToken = default) =>
        Task.FromResult(_workers.Any(worker =>
            string.Equals(worker.ExternalRef, externalRef, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyDictionary<string, Guid>> GetIdsByExternalRefsAsync(
        IReadOnlyCollection<string> externalRefs,
        CancellationToken cancellationToken = default)
    {
        var map = _workers
            .Where(worker => externalRefs.Contains(worker.ExternalRef, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(worker => worker.ExternalRef, worker => worker.Id, StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, Guid>>(map);
    }
}

internal sealed class InMemoryContractRepository : IContractRepository
{
    private readonly List<Contract> _contracts = [];

    public Task AddAsync(Contract contract, CancellationToken cancellationToken = default)
    {
        _contracts.Add(contract);

        return Task.CompletedTask;
    }

    public Task<Contract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_contracts.FirstOrDefault(contract => contract.Id == contractId));

    public Task<Contract?> GetEffectiveContractAsync(
        Guid workerId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_contracts
            .Where(contract => contract.WorkerId == workerId && contract.IsEffectiveOn(effectiveOn))
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefault());

    public Task<bool> HasOverlappingContractAsync(
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_contracts.Any(contract =>
            contract.WorkerId == workerId
            && Contract.RangesOverlap(contract.StartDate, contract.EndDate, startDate, endDate)));
}

internal sealed class InMemoryTimesheetImportRepository : ITimesheetImportRepository
{
    private readonly List<TimesheetImport> _imports = [];

    public Task AddAsync(TimesheetImport import, CancellationToken cancellationToken = default)
    {
        _imports.Add(import);

        return Task.CompletedTask;
    }

    public Task<TimesheetImport?> GetByIdAsync(Guid importId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_imports.FirstOrDefault(import => import.Id == importId));

    public Task<bool> ExistsByChecksumAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        string sha256,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_imports.Any(import =>
            import.PeriodStart == periodStart && import.PeriodEnd == periodEnd && import.Sha256 == sha256));
}

internal sealed class InMemoryTimeEntryRepository : ITimeEntryRepository
{
    private readonly List<TimeEntry> _entries = [];

    public IReadOnlyList<TimeEntry> All => _entries;

    public Task AddRangeAsync(IReadOnlyCollection<TimeEntry> entries, CancellationToken cancellationToken = default)
    {
        _entries.AddRange(entries);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TimeEntry>> GetForImportAsync(
        Guid importId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TimeEntry>>(_entries.Where(entry => entry.ImportId == importId).ToArray());

    public Task<IReadOnlyList<Guid>> GetWorkerIdsForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(_entries
            .Where(entry => importIds.Contains(entry.ImportId))
            .Select(entry => entry.WorkerId)
            .Distinct()
            .ToArray());

    public Task<IReadOnlyDictionary<Guid, decimal>> SumUnitsByWorkerForImportsAsync(
        IReadOnlyCollection<Guid> importIds,
        CancellationToken cancellationToken = default)
    {
        var sums = _entries
            .Where(entry => importIds.Contains(entry.ImportId))
            .GroupBy(entry => entry.WorkerId)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Units));

        return Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(sums);
    }
}

internal sealed class InMemoryPayrollRunRepository : IPayrollRunRepository
{
    private readonly List<PayrollRun> _runs = [];
    private readonly List<PayrollRunVersion> _versions = [];

    public Task AddAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        _runs.Add(run);

        return Task.CompletedTask;
    }

    public Task AddVersionAsync(PayrollRunVersion version, CancellationToken cancellationToken = default)
    {
        _versions.Add(version);

        return Task.CompletedTask;
    }

    public Task<PayrollRun?> GetByIdAsync(Guid payrollRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.FirstOrDefault(run => run.Id == payrollRunId));

    public Task<PayrollRunVersion?> GetVersionWithPaymentsAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_versions.FirstOrDefault(version => version.Id == versionId));

    public Task<PayrollRunVersion?> GetLatestVersionWithPaymentsAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_versions
            .Where(version => version.PayrollRunId == payrollRunId)
            .OrderByDescending(version => version.Version)
            .FirstOrDefault());
}

internal sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> All => _events;

    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(auditEvent);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> SearchAsync(
        AuditEventFilter filter,
        string? afterCursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(_events
            .OrderByDescending(auditEvent => auditEvent.OccurredAtUtc)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(limit)
            .ToArray());
}

internal sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public Task<BlobUploadTicket> CreateUploadTicketAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BlobUploadTicket(
            new Uri($"https://blob.test/{container}/{blobName}"),
            container,
            blobName,
            DateTimeOffset.UtcNow.Add(lifetime)));

    public Task UploadAsync(
        string container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _blobs[$"{container}/{blobName}"] = buffer.ToArray();

        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryGetValue($"{container}/{blobName}", out var content))
        {
            throw new FileNotFoundException($"Blob '{container}/{blobName}' was not found.");
        }

        return Task.FromResult<Stream>(new MemoryStream(content));
    }

    public Task<Uri> CreateReadUrlAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new Uri($"https://blob.test/{container}/{blobName}?sig=test"));

    public void Seed(string container, string blobName, byte[] content) =>
        _blobs[$"{container}/{blobName}"] = content;
}

internal sealed class StubIdempotencyStore : IIdempotencyStore
{
    public Task<IdempotencyRecord?> FindAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IdempotencyRecord?>(null);

    public Task<IdempotencyBeginResult> BeginAsync(
        Guid tenantId,
        string operationCode,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new IdempotencyBeginResult(
            new IdempotencyRecord(
                Guid.NewGuid(),
                tenantId,
                operationCode,
                idempotencyKey,
                requestFingerprint,
                IdempotencyOutcome.InProgress,
                null,
                null,
                DateTimeOffset.UtcNow,
                null),
            true));

    public Task CompleteAsync(
        Guid recordId,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task AbandonAsync(Guid recordId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
