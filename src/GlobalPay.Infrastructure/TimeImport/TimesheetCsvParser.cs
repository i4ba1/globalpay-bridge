using System.Globalization;
using System.Text;
using GlobalPay.Application.Abstractions.TimeImport;

namespace GlobalPay.Infrastructure.TimeImport;

internal sealed class TimesheetCsvParser : ITimesheetCsvParser
{
    private const string WorkerRefHeader = "workerref";

    private const string WorkDateHeader = "workdate";

    private const string UnitsHeader = "units";

    private const string ComponentCodeHeader = "componentcode";

    public TimesheetParseResult Parse(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var rows = new List<TimesheetRow>();
        var errors = new List<TimesheetParseError>();
        var totalDataRows = 0;

        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var indexes = ReadHeader(reader, errors);

        if (indexes is null)
        {
            return new TimesheetParseResult(rows, errors, totalDataRows);
        }

        var lineNumber = 1;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalDataRows++;

            var fields = SplitCsv(line);

            if (!TryReadRow(fields, indexes, lineNumber, errors, out var row))
            {
                continue;
            }

            rows.Add(row);
        }

        return new TimesheetParseResult(rows, errors, totalDataRows);
    }

    private static ColumnIndexes? ReadHeader(StreamReader reader, List<TimesheetParseError> errors)
    {
        string? header;

        do
        {
            header = reader.ReadLine();

            if (header is null)
            {
                errors.Add(new TimesheetParseError(1, "import.header_missing", "The file does not contain a header row."));

                return null;
            }
        }
        while (string.IsNullOrWhiteSpace(header));

        var headers = SplitCsv(header)
            .Select(value => value.Trim().ToLowerInvariant())
            .ToArray();

        var workerRef = Array.IndexOf(headers, WorkerRefHeader);
        var workDate = Array.IndexOf(headers, WorkDateHeader);
        var units = Array.IndexOf(headers, UnitsHeader);
        var componentCode = Array.IndexOf(headers, ComponentCodeHeader);

        if (workerRef < 0 || workDate < 0 || units < 0)
        {
            errors.Add(new TimesheetParseError(
                1,
                "import.header_invalid",
                "The header must contain the columns: workerRef, workDate, units, and optionally componentCode."));

            return null;
        }

        return new ColumnIndexes(workerRef, workDate, units, componentCode);
    }

    private static bool TryReadRow(
        List<string> fields,
        ColumnIndexes indexes,
        int lineNumber,
        List<TimesheetParseError> errors,
        out TimesheetRow row)
    {
        row = null!;

        var requiredIndex = Math.Max(indexes.WorkerRef, Math.Max(indexes.WorkDate, indexes.Units));

        if (fields.Count <= requiredIndex)
        {
            errors.Add(new TimesheetParseError(lineNumber, "import.row_column_count", "The row has fewer columns than required."));

            return false;
        }

        var workerRef = fields[indexes.WorkerRef].Trim();

        if (string.IsNullOrWhiteSpace(workerRef))
        {
            errors.Add(new TimesheetParseError(lineNumber, "import.worker_ref_required", "workerRef is required."));

            return false;
        }

        if (!DateOnly.TryParse(fields[indexes.WorkDate].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var workDate))
        {
            errors.Add(new TimesheetParseError(lineNumber, "import.date_invalid", "workDate must be a valid date (yyyy-MM-dd)."));

            return false;
        }

        if (!decimal.TryParse(
                fields[indexes.Units].Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var units)
            || units <= 0m)
        {
            errors.Add(new TimesheetParseError(lineNumber, "import.units_invalid", "units must be a positive number."));

            return false;
        }

        string? componentCode = null;

        if (indexes.ComponentCode >= 0 && fields.Count > indexes.ComponentCode)
        {
            var value = fields[indexes.ComponentCode].Trim();
            componentCode = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        row = new TimesheetRow(lineNumber, workerRef, workDate, units, componentCode);

        return true;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(character);
            }
        }

        fields.Add(builder.ToString());

        return fields;
    }

    private sealed record ColumnIndexes(int WorkerRef, int WorkDate, int Units, int ComponentCode);
}
