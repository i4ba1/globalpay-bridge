using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Workforce.Commands;
using GlobalPay.Application.Modules.Workforce.Queries;
using GlobalPay.Contracts.Workers;

using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class WorkerEndpoints
{
    public static IEndpointRouteBuilder MapWorkerEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/workers").WithTags("Workers");

        group.MapPost("/", CreateWorkerAsync)
            .AddEndpointFilter<ValidationEndpointFilter<CreateWorkerRequest>>()
            .RequireAuthorization(Policies.Admin)
            .WithName("CreateWorker")
            .WithSummary("Create a worker")
            .Produces<WorkerResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetWorkerAsync)
            .RequireAuthorization()
            .WithName("GetWorker")
            .WithSummary("Read a worker")
            .Produces<WorkerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/contracts", CreateContractAsync)
            .AddEndpointFilter<ValidationEndpointFilter<CreateContractRequest>>()
            .RequireAuthorization(Policies.Admin)
            .WithName("CreateContract")
            .WithSummary("Create an effective-dated contract")
            .Produces<ContractResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateWorkerAsync(
        CreateWorkerRequest request,
        [FromServices] ICommandHandler<CreateWorkerCommand, WorkerResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler
            .HandleAsync(new CreateWorkerCommand(request.ExternalRef, request.DisplayName, request.Email), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/workers/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetWorkerAsync(
        Guid id,
        [FromServices] IQueryHandler<GetWorkerQuery, WorkerResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetWorkerQuery(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> CreateContractAsync(
        Guid id,
        CreateContractRequest request,
        [FromServices] ICommandHandler<CreateContractCommand, ContractResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateContractCommand(id, request.StartDate, request.EndDate, request.Currency, request.Components);

        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/workers/{id}/contracts/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }
}
