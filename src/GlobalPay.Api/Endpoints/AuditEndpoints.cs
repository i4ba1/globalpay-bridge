using GlobalPay.Api.Composition;
using GlobalPay.Api.Errors;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.Audit.Queries;
using GlobalPay.Contracts.Audit;
using GlobalPay.Contracts.Common;

using Microsoft.AspNetCore.Mvc;

namespace GlobalPay.Api.Endpoints;

internal static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/audit-events", SearchAsync)
            .WithTags("Audit")
            .RequireAuthorization(Policies.Auditor)
            .WithName("SearchAuditEvents")
            .WithSummary("Search the immutable audit timeline")
            .Produces<PagedResponse<AuditEventResponse>>();

        return app;
    }

    private static async Task<IResult> SearchAsync(
        [FromServices] IQueryHandler<SearchAuditEventsQuery, PagedResponse<AuditEventResponse>> handler,
        CancellationToken cancellationToken,
        string? actor = null,
        string? action = null,
        string? targetType = null,
        Guid? targetId = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? cursor = null,
        int limit = 50)
    {
        var query = new SearchAuditEventsQuery(
            actor,
            action,
            targetType,
            targetId,
            fromUtc,
            toUtc,
            cursor,
            limit);

        var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
