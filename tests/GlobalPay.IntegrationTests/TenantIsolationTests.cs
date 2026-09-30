using GlobalPay.Domain.Modules.Audit;
using GlobalPay.Infrastructure.Persistence;
using GlobalPay.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace GlobalPay.IntegrationTests;

public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Queries_Are_Scoped_To_The_Current_Tenant()
    {
        var options = BuildOptions();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(options, tenantA, tenantB);

        await using var readerContext = new GlobalPayDbContext(options, CreateTenant(tenantA));

        var visible = await readerContext.AuditEvents.ToListAsync();

        var only = Assert.Single(visible);
        Assert.Equal(tenantA, only.TenantId);
    }

    [Fact]
    public async Task CrossTenant_Read_Returns_No_Foreign_Rows()
    {
        var options = BuildOptions();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(options, tenantA, tenantB);

        await using var readerContext = new GlobalPayDbContext(options, CreateTenant(tenantB));

        var visible = await readerContext.AuditEvents.ToListAsync();

        Assert.DoesNotContain(visible, auditEvent => auditEvent.TenantId == tenantA);
        Assert.All(visible, auditEvent => Assert.Equal(tenantB, auditEvent.TenantId));
    }

    [Fact]
    public async Task Without_Tenant_Context_System_Operations_See_All_Rows()
    {
        var options = BuildOptions();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await SeedAsync(options, tenantA, tenantB);

        await using var systemContext = new GlobalPayDbContext(options, CreateTenant(null));

        var visible = await systemContext.AuditEvents.ToListAsync();

        Assert.Equal(2, visible.Count);
    }

    private static async Task SeedAsync(
        DbContextOptions<GlobalPayDbContext> options,
        Guid tenantA,
        Guid tenantB)
    {
        await using var seedContext = new GlobalPayDbContext(options, CreateTenant(null));

        seedContext.AuditEvents.Add(AuditEvent.Record(
            tenantA, "user-a", "worker.created", "Worker", Guid.NewGuid(), DateTimeOffset.UtcNow));
        seedContext.AuditEvents.Add(AuditEvent.Record(
            tenantB, "user-b", "worker.created", "Worker", Guid.NewGuid(), DateTimeOffset.UtcNow));

        await seedContext.SaveChangesAsync();
    }

    private static DbContextOptions<GlobalPayDbContext> BuildOptions()
    {
        var databaseName = $"globalpay-tests-{Guid.NewGuid():N}";

        return new DbContextOptionsBuilder<GlobalPayDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private static TenantContext CreateTenant(Guid? tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        return tenantContext;
    }
}
