using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Application.Modules.TimeImport.Queries;
using GlobalPay.Contracts.TimeImports;

using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class TimesheetImportEndpoints
{
    public static IEndpointRouteBuilder MapTimesheetImportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/timesheet-imports").WithTags("Timesheet imports");

        group.MapPost("/", RegisterImportAsync)
            .AddEndpointFilter<ValidationEndpointFilter<RegisterTimesheetImportRequest>>()
            .RequireAuthorization(Policies.Admin)
            .WithName("RegisterTimesheetImport")
            .WithSummary("Register a timesheet import and obtain an upload location")
            .Produces<TimesheetImportRegistrationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetImportAsync)
            .RequireAuthorization()
            .WithName("GetTimesheetImport")
            .WithSummary("Read the processing result of a timesheet import")
            .Produces<TimesheetImportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterImportAsync(
        RegisterTimesheetImportRequest request,
        [FromServices] ICommandHandler<RegisterTimesheetImportCommand, TimesheetImportRegistrationResponse> handler,
        CancellationToken cancellationToken)
    {
        var command = new RegisterTimesheetImportCommand(
            request.PeriodStart,
            request.PeriodEnd,
            request.FileName,
            request.Sha256,
            request.SizeBytes);

        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Created($"/api/v1/timesheet-imports/{result.Value.ImportId}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> GetImportAsync(
        Guid id,
        [FromServices] IQueryHandler<GetTimesheetImportQuery, TimesheetImportResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTimesheetImportQuery(id), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToProblem();
    }
}
