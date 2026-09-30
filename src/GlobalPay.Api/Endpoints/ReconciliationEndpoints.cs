using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Reconciliation;
using GlobalPay.Contracts.Common;
using GlobalPay.Contracts.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/reconciliation-cases").WithTags("Reconciliation");

        group.MapGet("/", GetCasesAsync)
            .RequireAuthorization(Policies.FinanceOrAuditor)
            .WithName("GetReconciliationCases")
            .WithSummary("List payment reconciliation exceptions")
            .Produces<PagedResponse<ReconciliationCaseResponse>>();

        group.MapPost("/{id:guid}/resolution", ResolveCaseAsync)
            .AddEndpointFilter<ValidationEndpointFilter<ResolveReconciliationCaseRequest>>()
            .RequireAuthorization(Policies.Finance)
            .WithName("ResolveReconciliationCase")
            .WithSummary("Resolve or write off a reconciliation exception")
            .Produces<ReconciliationCaseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetCasesAsync(
        [FromServices] IQueryHandler<GetReconciliationCasesQuery, PagedResponse<ReconciliationCaseResponse>> handler,
        CancellationToken cancellationToken,
        string? status = null,
        string? reason = null,
        Guid? paymentBatchId = null,
        string? cursor = null,
        int limit = 50)
    {
        var query = new GetReconciliationCasesQuery(status, reason, paymentBatchId, cursor, limit);

        var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ResolveCaseAsync(
        Guid id,
        ResolveReconciliationCaseRequest request,
        [FromServices] ICommandHandler<ResolveReconciliationCaseCommand, ReconciliationCaseResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler
            .HandleAsync(new ResolveReconciliationCaseCommand(id, request.WriteOff, request.Note), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
