using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Payments;
using GlobalPay.Contracts.Payments;
using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class PaymentEndpoints
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/payroll-runs/{id:guid}/payment-batches", CreateBatchAsync)
            .WithTags("Payments")
            .RequireAuthorization(Policies.Finance)
            .WithName("CreatePaymentBatch")
            .WithSummary("Create and queue a payment batch for an approved payroll run")
            .Produces<PaymentBatchResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/payment-batches/{id:guid}", GetBatchAsync)
            .WithTags("Payments")
            .RequireAuthorization(Policies.FinanceOrApprover)
            .WithName("GetPaymentBatch")
            .WithSummary("Read the status of a payment batch")
            .Produces<PaymentBatchResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateBatchAsync(
        Guid id,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        [FromServices] ICommandHandler<CreatePaymentBatchCommand, PaymentBatchResponse> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                title: "Idempotency-Key required",
                detail: $"The '{IdempotencyKeyHeader}' header is required for payment submission.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key_required" });
        }

        var result = await handler
            .HandleAsync(new CreatePaymentBatchCommand(id, idempotencyKey), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/payment-batches/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetBatchAsync(
        Guid id,
        [FromServices] IQueryHandler<GetPaymentBatchQuery, PaymentBatchResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPaymentBatchQuery(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
