using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Payroll.Commands;
using GlobalPay.Application.Modules.Payroll.Queries;
using GlobalPay.Contracts.Payroll;

using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/payroll-runs").WithTags("Payroll");

        group.MapPost("/", CreateRunAsync)
            .AddEndpointFilter<ValidationEndpointFilter<CreatePayrollRunRequest>>()
            .RequireAuthorization(Policies.Admin)
            .WithName("CreatePayrollRun")
            .WithSummary("Create a payroll run")
            .Produces<PayrollRunResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetRunAsync)
            .RequireAuthorization()
            .WithName("GetPayrollRun")
            .WithSummary("Read a payroll run with its current calculation snapshot")
            .Produces<PayrollRunResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/calculate", CalculateAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("CalculatePayrollRun")
            .WithSummary("Calculate a deterministic payroll snapshot")
            .Produces<PayrollRunResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("SubmitPayrollRun")
            .WithSummary("Submit a calculated run for approval")
            .Produces<PayrollRunResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/decisions", DecideAsync)
            .AddEndpointFilter<ValidationEndpointFilter<PayrollDecisionRequest>>()
            .RequireAuthorization(Policies.Approver)
            .WithName("DecidePayrollRun")
            .WithSummary("Approve or reject a submitted run (maker-checker)")
            .Produces<PayrollRunResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/variance", GetVarianceAsync)
            .RequireAuthorization(Policies.AdminOrApprover)
            .WithName("GetPayrollVariance")
            .WithSummary("Read the variance report against the prior period")
            .Produces<PayrollVarianceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateRunAsync(
        CreatePayrollRunRequest request,
        [FromServices] ICommandHandler<CreatePayrollRunCommand, PayrollRunResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new CreatePayrollRunCommand(
            request.PeriodStart,
            request.PeriodEnd,
            request.Currency,
            request.SourceImportIds,
            request.Note);

        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/payroll-runs/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetRunAsync(
        Guid id,
        [FromServices] IQueryHandler<GetPayrollRunQuery, PayrollRunResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPayrollRunQuery(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> CalculateAsync(
        Guid id,
        [FromServices] ICommandHandler<CalculatePayrollRunCommand, PayrollRunResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CalculatePayrollRunCommand(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> SubmitAsync(
        Guid id,
        [FromServices] ICommandHandler<SubmitPayrollRunCommand, PayrollRunResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SubmitPayrollRunCommand(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> DecideAsync(
        Guid id,
        PayrollDecisionRequest request,
        [FromServices] ICommandHandler<DecidePayrollRunCommand, PayrollRunResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler
            .HandleAsync(new DecidePayrollRunCommand(id, request.Approve, request.Reason), cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetVarianceAsync(
        Guid id,
        [FromServices] IQueryHandler<GetPayrollVarianceQuery, PayrollVarianceResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPayrollVarianceQuery(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
