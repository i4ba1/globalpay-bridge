namespace GlobalPay.Application.Modules.TimeImport;

public static class ImportBlobNames
{
    public const string Container = StorageContainers.TimesheetImports;

    public static string Build(Guid tenantId, DateOnly periodEnd, Guid importId, string fileName) =>
        $"{tenantId:D}/{periodEnd:yyyy-MM}/{importId:D}/{fileName}";

    public static bool TryParseImportId(string blobName, out Guid importId)
    {
        importId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(blobName))
        {
            return false;
        }

        var segments = blobName.Split('/');

        return segments.Length >= 3 && Guid.TryParse(segments[2], out importId);
    }
}
