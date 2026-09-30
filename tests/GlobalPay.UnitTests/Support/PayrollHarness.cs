using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.TimeImport;
using GlobalPay.Application.Modules.Payments;
using GlobalPay.Application.Modules.Payroll.Commands;
using GlobalPay.Application.Modules.Reconciliation;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Application.Modules.Workforce.Commands;
using GlobalPay.Application.Modules.Workforce.Queries;
using GlobalPay.Contracts.Common;
using GlobalPay.Contracts.Payments;
using GlobalPay.Contracts.Payroll;
using GlobalPay.Contracts.Reconciliation;
using GlobalPay.Contracts.TimeImports;
using GlobalPay.Contracts.Workers;
using GlobalPay.Infrastructure.TimeImport;
using Microsoft.Extensions.Logging.Abstractions;

namespace GlobalPay.UnitTests.Support;

internal sealed class PayrollHarness
{
    private InMemoryOutboxWriter? _outboxWriter;

    public PayrollHarness(Guid? tenantId)
    {
        Tenant.SetTenant(tenantId);
    }

    public TestTenantContext Tenant { get; } = new();

    public TestCurrentUser User { get; } = new();

    public TestAccessPolicy Access { get; } = new();

    public TestUnitOfWork UnitOfWork { get; } = new();

    public TestTimeProvider Clock { get; } = new();

    public RecordingAuditRecorder Audit { get; } = new();

    public InMemoryWorkerRepository Workers { get; } = new();

    public InMemoryContractRepository Contracts { get; } = new();

    public InMemoryTimesheetImportRepository Imports { get; } = new();

    public InMemoryTimeEntryRepository TimeEntries { get; } = new();

    public InMemoryPayrollRunRepository Runs { get; } = new();

    public InMemoryAuditEventRepository AuditEvents { get; } = new();

    public InMemoryBlobStorage Blobs { get; } = new();

    public InMemoryPaymentBatchRepository PaymentBatches { get; } = new();

    public InMemoryReconciliationCaseRepository ReconciliationCases { get; } = new();

    public InMemoryOutboxStore Outbox { get; } = new();

    public InMemoryInboxStore Inbox { get; } = new();

    public InMemoryIdempotencyStore Idempotency { get; } = new();

    public FakePaymentProvider PaymentProvider { get; } = new();

    public TestSignatureVerifier SignatureVerifier { get; } = new();

    public ITimesheetCsvParser Parser { get; } = new TimesheetCsvParser();

    public InMemoryOutboxWriter OutboxWriter => _outboxWriter ??= new InMemoryOutboxWriter(Outbox);

    public ICommandHandler<CreateWorkerCommand, WorkerResponse> CreateWorkerHandler =>
        new CreateWorkerCommandHandler(Workers, Tenant, Access, UnitOfWork, Clock);

    public IQueryHandler<GetWorkerQuery, WorkerResponse> GetWorkerHandler =>
        new GetWorkerQueryHandler(Workers, Tenant);

    public ICommandHandler<CreateContractCommand, ContractResponse> CreateContractHandler =>
        new CreateContractCommandHandler(Workers, Contracts, Tenant, Access, UnitOfWork, Clock);

    public ICommandHandler<RegisterTimesheetImportCommand, TimesheetImportRegistrationResponse> RegisterImportHandler =>
        new RegisterTimesheetImportCommandHandler(Imports, Blobs, Tenant, Access, UnitOfWork, Clock);

    public ICommandHandler<ProcessTimesheetImportCommand, ImportProcessingResultResponse> ProcessImportHandler =>
        new ProcessTimesheetImportCommandHandler(
            Imports,
            TimeEntries,
            Workers,
            Parser,
            Blobs,
            Tenant,
            Audit,
            UnitOfWork,
            NullLogger<ProcessTimesheetImportCommandHandler>.Instance,
            Clock);

    public ICommandHandler<CreatePayrollRunCommand, PayrollRunResponse> CreateRunHandler =>
        new CreatePayrollRunCommandHandler(Runs, Imports, Tenant, User, Access, Audit, UnitOfWork, Clock);

    public ICommandHandler<CalculatePayrollRunCommand, PayrollRunResponse> CalculateHandler =>
        new CalculatePayrollRunCommandHandler(Runs, Contracts, TimeEntries, Tenant, User, Access, Audit, UnitOfWork, Clock);

    public ICommandHandler<SubmitPayrollRunCommand, PayrollRunResponse> SubmitHandler =>
        new SubmitPayrollRunCommandHandler(Runs, Tenant, User, Access, Audit, UnitOfWork, Clock);

    public ICommandHandler<DecidePayrollRunCommand, PayrollRunResponse> DecideHandler =>
        new DecidePayrollRunCommandHandler(Runs, Tenant, User, Access, Audit, UnitOfWork, Clock);

    public ICommandHandler<CreatePaymentBatchCommand, PaymentBatchResponse> CreateBatchHandler =>
        new CreatePaymentBatchCommandHandler(
            Runs,
            PaymentBatches,
            Idempotency,
            OutboxWriter,
            Tenant,
            User,
            Access,
            Audit,
            UnitOfWork,
            Clock);

    public IQueryHandler<GetPaymentBatchQuery, PaymentBatchResponse> GetBatchHandler =>
        new GetPaymentBatchQueryHandler(PaymentBatches, Tenant);

    public ICommandHandler<DispatchOutboxCommand, OutboxDispatchResult> DispatchHandler =>
        new DispatchOutboxCommandHandler(
            Outbox,
            PaymentBatches,
            PaymentProvider,
            Audit,
            UnitOfWork,
            NullLogger<DispatchOutboxCommandHandler>.Instance,
            Clock);

    public ICommandHandler<ProcessPaymentWebhookCommand, Unit> WebhookHandler =>
        new ProcessPaymentWebhookCommandHandler(
            PaymentBatches,
            Runs,
            SignatureVerifier,
            Inbox,
            Tenant,
            Audit,
            UnitOfWork,
            NullLogger<ProcessPaymentWebhookCommandHandler>.Instance,
            Clock);

    public ICommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult> ReconcileHandler =>
        new ReconcilePaymentsCommandHandler(PaymentBatches, ReconciliationCases, Audit, UnitOfWork, Clock);

    public IQueryHandler<GetReconciliationCasesQuery, PagedResponse<ReconciliationCaseResponse>> GetCasesHandler =>
        new GetReconciliationCasesQueryHandler(ReconciliationCases, Tenant, Access);

    public ICommandHandler<ResolveReconciliationCaseCommand, ReconciliationCaseResponse> ResolveCaseHandler =>
        new ResolveReconciliationCaseCommandHandler(
            ReconciliationCases,
            Tenant,
            User,
            Access,
            Audit,
            UnitOfWork,
            Clock);
}
