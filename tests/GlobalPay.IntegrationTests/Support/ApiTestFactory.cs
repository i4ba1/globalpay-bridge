using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GlobalPay.IntegrationTests.Support;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GlobalPay"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=GlobalPayBridgeTests;Trusted_Connection=True;TrustServerCertificate=True",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["ConnectionStrings:Blobs"] = "UseDevelopmentStorage=true",
                ["Authentication:Authority"] = string.Empty,
            }));

        builder.ConfigureServices(services =>
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }
}
