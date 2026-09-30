using System.Reflection;
using GlobalPay.Application.Abstractions;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.Audit;
using GlobalPay.Domain.Modules.Payments;
using GlobalPay.Domain.Modules.Reconciliation;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Domain.Modules.Tenancy;
using GlobalPay.Domain.Modules.TimeImport;
using GlobalPay.Domain.Modules.Workforce;
using GlobalPay.Infrastructure.Persistence.Idempotency;
using GlobalPay.Infrastructure.Persistence.Inbox;
using GlobalPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.Infrastructure.Persistence;

public sealed class GlobalPayDbContext : DbContext
{
    private static readonly MethodInfo SetTenantQueryFilterMethod = typeof(GlobalPayDbContext)
        .GetMethod(nameof(SetTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContext _tenantContext;

    public GlobalPayDbContext(DbContextOptions<GlobalPayDbContext> options, ITenantContext tenantContext)
        : base(options) => _tenantContext = tenantContext;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<UserTenantRole> UserTenantRoles => Set<UserTenantRole>();

    public DbSet<Worker> Workers => Set<Worker>();

    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<CompensationComponent> CompensationComponents => Set<CompensationComponent>();

    public DbSet<TimesheetImport> TimesheetImports => Set<TimesheetImport>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();

    public DbSet<PayrollRunVersion> PayrollRunVersions => Set<PayrollRunVersion>();

    public DbSet<WorkerPayment> WorkerPayments => Set<WorkerPayment>();

    public DbSet<WorkerPaymentLine> WorkerPaymentLines => Set<WorkerPaymentLine>();

    public DbSet<PaymentBatch> PaymentBatches => Set<PaymentBatch>();

    public DbSet<PaymentInstruction> PaymentInstructions => Set<PaymentInstruction>();

    public DbSet<IdempotencyRecordEntry> IdempotencyRecords => Set<IdempotencyRecordEntry>();

    public DbSet<ReconciliationCase> ReconciliationCases => Set<ReconciliationCase>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    private bool HasTenant => _tenantContext.HasTenant;

    private Guid? CurrentTenantId => _tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GlobalPayDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private void SetTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            !HasTenant || entity.TenantId == CurrentTenantId);
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            SetTenantQueryFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }
}
