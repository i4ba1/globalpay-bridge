var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "GlobalPay.ProviderSimulator", status = "healthy" }));

app.MapPost(
    "/api/v1/payments",
    (ProviderPaymentRequest request) =>
    {
        var providerReference = $"prov_{Guid.NewGuid():N}";

        return Results.Accepted(
            $"/api/v1/payments/{providerReference}",
            new ProviderPaymentResponse(providerReference, "accepted", request.PaymentBatchId));
    });

app.Run();

internal sealed record ProviderPaymentRequest(
    string PaymentBatchId,
    decimal Amount,
    string Currency,
    string Destination);

internal sealed record ProviderPaymentResponse(
    string ProviderReference,
    string Status,
    string PaymentBatchId);

public partial class Program;
