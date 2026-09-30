using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Payments;
using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class WebhookEndpoints
{
    private const string EventIdHeader = "X-Provider-Event-Id";

    private const string TimestampHeader = "X-Provider-Timestamp";

    private const string SignatureHeader = "X-Provider-Signature";

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/webhooks/payment-provider", ReceiveAsync)
            .WithTags("Webhooks")
            .AllowAnonymous()
            .WithName("ReceivePaymentWebhook")
            .WithSummary("Receive a signed payment provider event")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest httpRequest,
        [FromServices] ICommandHandler<ProcessPaymentWebhookCommand, Unit> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);

        using var reader = new StreamReader(httpRequest.Body);

        var rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var eventId = httpRequest.Headers[EventIdHeader].FirstOrDefault();
        var signature = httpRequest.Headers[SignatureHeader].FirstOrDefault();
        var timestamp = httpRequest.Headers[TimestampHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(signature))
        {
            return Results.Problem(
                title: "Missing webhook headers",
                detail: $"'{EventIdHeader}' and '{SignatureHeader}' are required.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "webhook.headers_missing" });
        }

        if (!long.TryParse(timestamp, System.Globalization.CultureInfo.InvariantCulture, out var unixTimestamp))
        {
            return Results.Problem(
                title: "Invalid webhook timestamp",
                detail: $"'{TimestampHeader}' must be a Unix timestamp in seconds.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "webhook.timestamp_invalid" });
        }

        var result = await handler
            .HandleAsync(new ProcessPaymentWebhookCommand(eventId, unixTimestamp, signature, rawBody), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Accepted()
            : result.Error.ToProblem();
    }
}
