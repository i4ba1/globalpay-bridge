using Azure.Identity;
using Azure.Storage.Blobs;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Idempotency;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Payments;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Application.Abstractions.Reporting;
using GlobalPay.Application.Abstractions.Storage;
using GlobalPay.Application.Abstractions.TimeImport;
using GlobalPay.Infrastructure.Audit;
using GlobalPay.Infrastructure.Idempotency;
using GlobalPay.Infrastructure.Identity;
using GlobalPay.Infrastructure.Messaging;
using GlobalPay.Infrastructure.Payments;
using GlobalPay.Infrastructure.Persistence;
using GlobalPay.Infrastructure.Persistence.Repositories;
using GlobalPay.Infrastructure.Reporting;
using GlobalPay.Infrastructure.Security;
using GlobalPay.Infrastructure.Storage;
using GlobalPay.Infrastructure.Tenancy;
using GlobalPay.Infrastructure.TimeImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalPay.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<GlobalPayDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("GlobalPay"),
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextMutator>(provider => provider.GetRequiredService<TenantContext>());

        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<IAccessPolicy, AccessPolicy>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdempotencyStore, SqlIdempotencyStore>();
        services.AddScoped<ITenantScopeFactory, TenantScopeFactory>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();

        services.AddScoped<IWorkerRepository, WorkerRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<ITimesheetImportRepository, TimesheetImportRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IPayrollRunRepository, PayrollRunRepository>();
        services.AddScoped<IPaymentBatchRepository, PaymentBatchRepository>();
        services.AddScoped<IReconciliationCaseRepository, ReconciliationCaseRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IPayrollReportingQueries, PayrollReportingQueries>();
        services.AddSingleton<ITimesheetCsvParser, TimesheetCsvParser>();

        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IInboxStore, InboxStore>();

        services.Configure<PaymentProviderOptions>(configuration.GetSection("Payments:Provider"));
        services.AddSingleton<IWebhookSignatureVerifier, HmacWebhookSignatureVerifier>();

        var providerOptions = configuration.GetSection("Payments:Provider").Get<PaymentProviderOptions>()
            ?? new PaymentProviderOptions();

        services
            .AddHttpClient<IPaymentProvider, PaymentProviderClient>(client =>
            {
                client.BaseAddress = new Uri(providerOptions.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        });

        services.AddSingleton(CreateBlobServiceClient(configuration));
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton<IBlobStorage, AzureBlobStorage>();

        return services;
    }

    private static BlobServiceClient CreateBlobServiceClient(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Blobs");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobServiceClient(connectionString);
        }

        var serviceUri = configuration["Blobs:ServiceUri"];
        if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            return new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());
        }

        throw new InvalidOperationException(
            "Blob storage is not configured. Provide ConnectionStrings:Blobs or Blobs:ServiceUri.");
    }
}
