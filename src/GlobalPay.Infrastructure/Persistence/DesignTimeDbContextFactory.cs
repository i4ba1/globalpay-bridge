using GlobalPay.Application.Abstractions;
using GlobalPay.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GlobalPay.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GlobalPayDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=GlobalPayBridge;Trusted_Connection=True;TrustServerCertificate=True";

    public GlobalPayDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GlobalPayDbContext>();
        optionsBuilder.UseSqlServer(DesignTimeConnectionString);

        ITenantContext tenantContext = new TenantContext();

        return new GlobalPayDbContext(optionsBuilder.Options, tenantContext);
    }
}
