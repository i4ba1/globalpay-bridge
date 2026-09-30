using System.Net;
using System.Net.Http.Json;
using GlobalPay.Contracts.Workers;
using GlobalPay.IntegrationTests.Support;
using GlobalPay.Domain.Modules.Tenancy;

namespace GlobalPay.IntegrationTests;

public sealed class ApiPipelineTests : IClassFixture<ApiTestFactory>
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ApiTestFactory _factory;

    public ApiPipelineTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_IsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/api/v1/workers/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorker_WithoutAdminRole_IsForbidden()
    {
        using var client = _factory.CreateClient();
        Authenticate(client, "finance.user", Roles.Finance);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/workers", UriKind.Relative),
            new CreateWorkerRequest("W-001", "Worker One", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateWorker_WithInvalidBody_IsValidationProblem()
    {
        using var client = _factory.CreateClient();
        Authenticate(client, "admin.user", Roles.Admin);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/workers", UriKind.Relative),
            new CreateWorkerRequest(string.Empty, string.Empty, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>();
        Assert.NotNull(problem);
        Assert.Equal("request.validation_failed", problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.Contains(problem.Errors.Keys, key => string.Equals(key, "externalRef", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuditTimeline_WithoutAuditorRole_IsForbidden()
    {
        using var client = _factory.CreateClient();
        Authenticate(client, "finance.user", Roles.Finance);

        var response = await client.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditTimeline_WithoutToken_IsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuditTimeline_WithAuditorRole_PassesAuthorization()
    {
        using var client = _factory.CreateClient();
        Authenticate(client, "auditor.user", Roles.Auditor);

        var response = await client.GetAsync(new Uri("/api/v1/audit-events?limit=not-a-number", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void Authenticate(HttpClient client, string userId, params string[] roles)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, TenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
    }

    private sealed record ValidationProblemPayload(string? Code, Dictionary<string, string[]>? Errors);
}
