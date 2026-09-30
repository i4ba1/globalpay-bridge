using System.Net;
using System.Net.Http.Json;
using GlobalPay.IntegrationTests.Support;

namespace GlobalPay.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ApiSmokeTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_Returns_Healthy_Service_Payload()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ServiceStatus>();
        Assert.NotNull(payload);
        Assert.Equal("GlobalPay.Api", payload.Service);
    }

    [Fact]
    public async Task Liveness_Probe_Returns_Healthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Response_Carries_Correlation_Id_Header()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task OpenApi_Document_Is_Generated()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record ServiceStatus(string Service, string Status);
}
