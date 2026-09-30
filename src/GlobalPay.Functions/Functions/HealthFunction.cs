using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GlobalPay.Functions.Functions;

public sealed class HealthFunction
{
    [Function("Health")]
    public static async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { service = "GlobalPay.Functions", status = "healthy" }).ConfigureAwait(false);

        return response;
    }
}
