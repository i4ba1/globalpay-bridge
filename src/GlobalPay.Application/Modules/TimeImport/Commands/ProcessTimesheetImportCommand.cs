using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GlobalPay.Application.Abstractions;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Abstractions.Persistence;
using GlobalPay.Application.Abstractions.Storage;
using GlobalPay.Application.Abstractions.TimeImport;
using GlobalPay.Contracts.TimeImports;
using GlobalPay.Domain.Common;
using GlobalPay.Domain.Modules.TimeImport;
using Microsoft.Extensions.Logging;

namespace GlobalPay.Application.Modules.TimeImport.Commands;

public sealed record ProcessTimesheetImportCommand(
    Guid ImportId,
    string BlobName) : ICommand<ImportProcessingResultResponse>;

internal sealed class ProcessTimesheetImportCommandHandler
    : ICommandHandler<ProcessTimesheetImportCommand, ImportProcessingResultResponse>
{
    private readonly ITimesheetImportRepository _imports;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkerRepository _workers;
    private readonly ITimesheetCsvParser _parser;
    private readonly IBlobStorage _blobStorage;
    private readonly ITenantContextMutator _tenantMutator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessTimesheetImportCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ProcessTimesheetImportCommandHandler(
        ITimesheetImportRepository imports,
        ITimeEntryRepository timeEntries,
        IWorkerRepository workers,
        ITimesheetCsvParser parser,
        IBlobStorage blobStorage,
        ITenantContextMutator tenantMutator,
        IAuditRecorder auditRecorder,
        IUnitOfWork unitOfWork,
        ILogger<ProcessTimesheetImportCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _imports = imports;
        _timeEntries = timeEntries;
        _workers = workers;
        _parser = parser;
        _blobStorage = blobStorage;
        _tenantMutator = tenantMutator;
        _auditRecorder = auditRecorder;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ImportProcessingResultResponse>> HandleAsync(
        ProcessTimesheetImportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var import = await _imports.GetByIdAsync(command.ImportId, cancellationToken).ConfigureAwait(false);

        if (import is null)
        {
            return Result.Failure<ImportProcessingResultResponse>(
                Error.NotFound("import.not_found", $"Import '{command.ImportId}' was not found."));
        }

        _tenantMutator.SetTenant(import.TenantId);

        if (import.Status is ImportStatus.Completed or ImportStatus.Failed)
        {
            return new ImportProcessingResultResponse(
                import.Id,
                import.Status.ToString(),
                import.TotalRows,
                import.ValidRows,
                import.InvalidRows,
                []);
        }

        try
        {
            import.MarkProcessing();
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var errors = await ProcessAsync(import, command.BlobName, cancellationToken).ConfigureAwait(false);

            return new ImportProcessingResultResponse(
                import.Id,
                import.Status.ToString(),
                import.TotalRows,
                import.ValidRows,
                import.InvalidRows,
                errors);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Timesheet import {ImportId} failed", command.ImportId);

            import.Fail(exception.Message, _timeProvider.GetUtcNow());

            await _auditRecorder
                .RecordAsync("timesheet.import.failed", nameof(TimesheetImport), import.Id, exception.Message, cancellationToken)
                .ConfigureAwait(false);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure<ImportProcessingResultResponse>(
                Error.Failure("import.failed", "The timesheet import failed. See the import record for details."));
        }
    }

    private async Task<IReadOnlyList<TimesheetImportRowErrorResponse>> ProcessAsync(
        TimesheetImport import,
        string blobName,
        CancellationToken cancellationToken)
    {
        await using var content = await _blobStorage
            .OpenReadAsync(ImportBlobNames.Container, blobName, cancellationToken)
            .ConfigureAwait(false);

        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
        buffered.Position = 0;

        var actualChecksum = Convert.ToHexString(await SHA256.HashDataAsync(buffered, cancellationToken).ConfigureAwait(false))
            .ToLowerInvariant();

        if (!string.Equals(actualChecksum, import.Sha256, StringComparison.Ordinal))
        {
            throw new DomainException(
                "The uploaded file checksum does not match the registered checksum.",
                "import.checksum_mismatch");
        }

        buffered.Position = 0;
        var parseResult = _parser.Parse(buffered);

        var externalRefs = parseResult.Rows
            .Select(row => row.WorkerExternalRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workerIds = await _workers
            .GetIdsByExternalRefsAsync(externalRefs, cancellationToken)
            .ConfigureAwait(false);

        var rowErrors = new List<TimesheetImportRowErrorResponse>();
        var entries = new List<TimeEntry>();

        foreach (var row in parseResult.Rows)
        {
            if (!workerIds.TryGetValue(row.WorkerExternalRef, out var workerId))
            {
                rowErrors.Add(new TimesheetImportRowErrorResponse(
                    row.LineNumber,
                    "import.unknown_worker",
                    $"Worker reference '{row.WorkerExternalRef}' is not known for this tenant."));
                continue;
            }

            if (row.WorkDate < import.PeriodStart || row.WorkDate > import.PeriodEnd)
            {
                rowErrors.Add(new TimesheetImportRowErrorResponse(
                    row.LineNumber,
                    "import.date_out_of_period",
                    $"Work date {row.WorkDate:yyyy-MM-dd} is outside the import period."));
                continue;
            }

            entries.Add(TimeEntry.Create(import.TenantId, import.Id, workerId, row.WorkDate, row.Units, row.ComponentCode));
        }

        foreach (var parseError in parseResult.Errors)
        {
            rowErrors.Add(new TimesheetImportRowErrorResponse(parseError.LineNumber, parseError.Code, parseError.Message));
        }

        if (entries.Count > 0)
        {
            await _timeEntries.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
        }

        string? errorReportBlobName = null;

        if (rowErrors.Count > 0)
        {
            errorReportBlobName = await WriteErrorReportAsync(import, blobName, rowErrors, cancellationToken)
                .ConfigureAwait(false);
        }

        import.Complete(
            parseResult.TotalDataRows,
            entries.Count,
            rowErrors.Count,
            errorReportBlobName,
            _timeProvider.GetUtcNow());

        await _auditRecorder
            .RecordAsync(
                "timesheet.import.completed",
                nameof(TimesheetImport),
                import.Id,
                $"valid={entries.Count};invalid={rowErrors.Count}",
                cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rowErrors;
    }

    private async Task<string> WriteErrorReportAsync(
        TimesheetImport import,
        string blobName,
        IReadOnlyList<TimesheetImportRowErrorResponse> errors,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder("line,code,message\n");

        foreach (var error in errors)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{error.LineNumber},{Escape(error.Code)},{Escape(error.Message)}\n");
        }

        var errorBlobName = $"{blobName}.errors.csv";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));

        await _blobStorage
            .UploadAsync(StorageContainers.ImportErrorReports, errorBlobName, stream, "text/csv", cancellationToken)
            .ConfigureAwait(false);

        return errorBlobName;
    }

    private static string Escape(string value) =>
        value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}
