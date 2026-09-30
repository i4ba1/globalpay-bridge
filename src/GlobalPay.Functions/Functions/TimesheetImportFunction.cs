using Azure.Storage.Blobs;
using GlobalPay.Application;
using GlobalPay.Application.Abstractions.Messaging;
using GlobalPay.Application.Modules.TimeImport;
using GlobalPay.Application.Modules.TimeImport.Commands;
using GlobalPay.Contracts.TimeImports;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GlobalPay.Functions.Functions;

public sealed class TimesheetImportFunction
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TimesheetImportFunction> _logger;

    public TimesheetImportFunction(IServiceScopeFactory scopeFactory, ILogger<TimesheetImportFunction> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Function("TimesheetImport")]
    public async Task RunAsync(
        [BlobTrigger(StorageContainers.TimesheetImports + "/{name}", Connection = "Blobs")] BlobClient blob,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(blob);

        if (!ImportBlobNames.TryParseImportId(blob.Name, out var importId))
        {
            _logger.LogWarning("Ignoring blob {BlobName}: it does not contain a timesheet import identifier", blob.Name);

            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ProcessTimesheetImportCommand, ImportProcessingResultResponse>>();

        var result = await handler
            .HandleAsync(new ProcessTimesheetImportCommand(importId, blob.Name), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Timesheet import {ImportId} reported failure {Code}",
                importId,
                result.Error.Code);

            return;
        }

        _logger.LogInformation(
            "Timesheet import {ImportId} finished with status {Status}: {Valid} valid and {Invalid} invalid rows",
            importId,
            result.Value.Status,
            result.Value.ValidRows,
            result.Value.InvalidRows);
    }
}
