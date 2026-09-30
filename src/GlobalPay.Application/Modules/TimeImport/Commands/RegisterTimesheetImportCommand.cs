using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Application.Abstractions.Storage;
using GlobalPay.Contracts.TimeImports;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.TimeImport;

namespace GlobalPay.Application.Modules.TimeImport.Commands;

public sealed record RegisterTimesheetImportCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string FileName,
    string Sha256,
    long SizeBytes) : ICommand<TimesheetImportRegistrationResponse>;

internal sealed class RegisterTimesheetImportCommandHandler
    : ICommandHandler<RegisterTimesheetImportCommand, TimesheetImportRegistrationResponse>
{
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(30);

    private readonly ITimesheetImportRepository _imports;
    private readonly IBlobStorage _blobStorage;
    private readonly ITenantContext _tenantContext;
    private readonly IAccessPolicy _accessPolicy;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RegisterTimesheetImportCommandHandler(
        ITimesheetImportRepository imports,
        IBlobStorage blobStorage,
        ITenantContext tenantContext,
        IAccessPolicy accessPolicy,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _imports = imports;
        _blobStorage = blobStorage;
        _tenantContext = tenantContext;
        _accessPolicy = accessPolicy;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<TimesheetImportRegistrationResponse>> HandleAsync(
        RegisterTimesheetImportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_tenantContext.HasTenant)
        {
            return Result.Failure<TimesheetImportRegistrationResponse>(
                Error.Unauthorized("tenant.required", "A tenant context is required."));
        }

        if (!_accessPolicy.CanManageWorkforce)
        {
            return Result.Failure<TimesheetImportRegistrationResponse>(
                Error.Forbidden("auth.forbidden", "You are not allowed to register timesheet imports."));
        }

        var checksum = command.Sha256.Trim().ToLowerInvariant();

        if (await _imports
                .ExistsByChecksumAsync(command.PeriodStart, command.PeriodEnd, checksum, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<TimesheetImportRegistrationResponse>(
                Error.Conflict("import.duplicate", "This file has already been imported for the period."));
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var importId = Guid.NewGuid();

        var blobName = ImportBlobNames.Build(tenantId, command.PeriodEnd, importId, command.FileName);

        var import = TimesheetImport.Register(
            tenantId,
            blobName,
            checksum,
            command.PeriodStart,
            command.PeriodEnd,
            _timeProvider.GetUtcNow(),
            importId);

        var ticket = await _blobStorage
            .CreateUploadTicketAsync(ImportBlobNames.Container, blobName, UploadLifetime, cancellationToken)
            .ConfigureAwait(false);

        await _imports.AddAsync(import, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TimesheetImportRegistrationResponse(
            import.Id,
            blobName,
            ticket.UploadUri,
            ticket.ExpiresAtUtc);
    }
}
