using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Contracts.TimeImports;
using GlobalPay.Domain.Common;

namespace GlobalPay.Application.Modules.TimeImport.Queries;

public sealed record GetTimesheetImportQuery(Guid ImportId) : IQuery<TimesheetImportResponse>;

internal sealed class GetTimesheetImportQueryHandler : IQueryHandler<GetTimesheetImportQuery, TimesheetImportResponse>
{
    private readonly ITimesheetImportRepository _imports;
    private readonly ITenantContext _tenantContext;

    public GetTimesheetImportQueryHandler(ITimesheetImportRepository imports, ITenantContext tenantContext)
    {
        _imports = imports;
        _tenantContext = tenantContext;
    }

    public async Task<Result<TimesheetImportResponse>> HandleAsync(
        GetTimesheetImportQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<TimesheetImportResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        var import = await _imports.GetByIdAsync(query.ImportId, cancellationToken).ConfigureAwait(false);

        if (import is null)
        {
            return Result.Failure<TimesheetImportResponse>(
                Error.NotFound("import.not_found", $"Import '{query.ImportId}' was not found."));
        }

        return TimeImportMapper.ToResponse(import);
    }
}
