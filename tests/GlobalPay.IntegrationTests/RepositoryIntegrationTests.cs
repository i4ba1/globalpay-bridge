using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Domain.Finance;
using GlobalPay.Domain.Modules.Audit;
using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Domain.Modules.Workforce;
using GlobalPay.Infrastructure.Persistence;
using GlobalPay.Infrastructure.Persistence.Repositories;
using GlobalPay.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.IntegrationTests;

public sealed class RepositoryIntegrationTests
{
    private static readonly Currency Usd = Currency.FromCode("USD");

    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WorkerRepository_FindsByExternalRef()
    {
        var (options, tenantId) = BuildOptions();
        await using var context = NewContext(options, tenantId);
        var repository = new WorkerRepository(context);

        await repository.AddAsync(Worker.Create(tenantId, "W-001", "Worker One", null, Now));
        await context.SaveChangesAsync();

        Assert.True(await repository.ExternalRefExistsAsync("W-001"));
        Assert.False(await repository.ExternalRefExistsAsync("W-404"));

        var map = await repository.GetIdsByExternalRefsAsync(["W-001"]);

        Assert.Single(map);
        Assert.True(map.ContainsKey("W-001"));
    }

    [Fact]
    public async Task ContractRepository_ResolvesEffectiveContractForDate()
    {
        var (options, tenantId) = BuildOptions();
        var workerId = Guid.NewGuid();

        await using (var seed = NewContext(options, tenantId))
        {
            var first = Contract.Create(tenantId, workerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), Usd, Now);
            first.AddComponent(CompensationType.Earning, "BASE", CalculationRule.HourlyRate, 10m);

            var second = Contract.Create(tenantId, workerId, new DateOnly(2026, 7, 1), null, Usd, Now);
            second.AddComponent(CompensationType.Earning, "BASE", CalculationRule.HourlyRate, 20m);

            seed.Contracts.AddRange(first, second);
            await seed.SaveChangesAsync();
        }

        await using var context = NewContext(options, tenantId);
        var repository = new ContractRepository(context);

        var effective = await repository.GetEffectiveContractAsync(workerId, new DateOnly(2026, 9, 30));

        Assert.NotNull(effective);
        Assert.Equal(new DateOnly(2026, 7, 1), effective.StartDate);
        Assert.Single(effective.Components);
        Assert.Equal(20m, effective.Components.First().Amount);

        Assert.True(await repository.HasOverlappingContractAsync(workerId, new DateOnly(2026, 5, 1), new DateOnly(2026, 8, 1)));
        Assert.False(await repository.HasOverlappingContractAsync(workerId, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public async Task PayrollRunRepository_LoadsLatestVersionWithPaymentsAndLines()
    {
        var (options, tenantId) = BuildOptions();
        Guid runId;

        await using (var seed = NewContext(options, tenantId))
        {
            var run = PayrollRun.Create(
                tenantId,
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 30),
                Usd,
                [Guid.NewGuid()],
                "admin.user",
                null,
                Now);

            runId = run.Id;
            seed.PayrollRuns.Add(run);

            var version = PayrollRunVersion.Create(tenantId, run.Id, 1, PayrollCalculator.RuleVersion, Usd, Now);
            version.AddPayment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                200m,
                20m,
                180m,
                [new PaymentLineSpec("BASE", CompensationType.Earning, CalculationRule.HourlyRate, 200m)]);
            version.Seal();

            seed.PayrollRunVersions.Add(version);
            await seed.SaveChangesAsync();
        }

        await using var context = NewContext(options, tenantId);
        var repository = new PayrollRunRepository(context);

        var loaded = await repository.GetLatestVersionWithPaymentsAsync(runId);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Version);
        Assert.Equal(200m, loaded.GrossTotal);
        Assert.Equal(180m, loaded.NetTotal);

        var payment = Assert.Single(loaded.Payments);
        Assert.Single(payment.Lines);
        Assert.Equal("BASE", payment.Lines.First().ComponentCode);
    }

    [Fact]
    public async Task AuditEventRepository_ReturnsNewestFirst_AndPagesByCursor()
    {
        var (options, tenantId) = BuildOptions();

        await using (var seed = NewContext(options, tenantId))
        {
            for (var index = 0; index < 3; index++)
            {
                seed.AuditEvents.Add(AuditEvent.Record(
                    tenantId,
                    "admin.user",
                    "payroll.run.created",
                    "PayrollRun",
                    Guid.NewGuid(),
                    Now.AddMinutes(index)));
            }

            await seed.SaveChangesAsync();
        }

        await using var context = NewContext(options, tenantId);
        var repository = new AuditEventRepository(context);

        var firstPage = await repository.SearchAsync(new AuditEventFilter(null, null, null, null, null, null), null, 2);

        Assert.Equal(2, firstPage.Count);
        Assert.True(firstPage[0].OccurredAtUtc > firstPage[1].OccurredAtUtc);

        var cursor = GlobalPay.Application.Abstractions.Pagination.CursorCodec.Encode(
            firstPage[^1].OccurredAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            firstPage[^1].Id);

        var secondPage = await repository.SearchAsync(new AuditEventFilter(null, null, null, null, null, null), cursor, 2);

        Assert.Single(secondPage);
        Assert.True(secondPage[0].OccurredAtUtc < firstPage[^1].OccurredAtUtc);
    }

    [Fact]
    public async Task TenantFilter_IsolatesNewEntities()
    {
        var (options, tenantA) = BuildOptions();
        var tenantB = Guid.NewGuid();

        await using (var seed = NewContext(options, tenantA))
        {
            seed.Workers.Add(Worker.Create(tenantA, "A-001", "Tenant A worker", null, Now));
            seed.Workers.Add(Worker.Create(tenantB, "B-001", "Tenant B worker", null, Now));
            await seed.SaveChangesAsync();
        }

        await using var contextA = NewContext(options, tenantA);
        var workerRepository = new WorkerRepository(contextA);

        Assert.True(await workerRepository.ExternalRefExistsAsync("A-001"));
        Assert.False(await workerRepository.ExternalRefExistsAsync("B-001"));
    }

    private static (DbContextOptions<GlobalPayDbContext> Options, Guid TenantId) BuildOptions()
    {
        var databaseName = $"globalpay-repo-{Guid.NewGuid():N}";

        var options = new DbContextOptionsBuilder<GlobalPayDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return (options, Guid.NewGuid());
    }

    private static GlobalPayDbContext NewContext(DbContextOptions<GlobalPayDbContext> options, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        return new GlobalPayDbContext(options, tenantContext);
    }
}
