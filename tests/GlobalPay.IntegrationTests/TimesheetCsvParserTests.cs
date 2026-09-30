using System.Text;
using GlobalPay.Infrastructure.TimeImport;

namespace GlobalPay.IntegrationTests;

public sealed class TimesheetCsvParserTests
{
    private readonly TimesheetCsvParser _parser = new();

    [Fact]
    public void Parse_ValidFile_ReturnsRows()
    {
        var content = """
            workerRef,workDate,units,componentCode
            W-001,2026-09-01,8,BASE
            W-002,2026-09-02,7.5,BASE
            """;

        var result = _parser.Parse(Stream(content));

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.TotalDataRows);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("W-001", result.Rows[0].WorkerExternalRef);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Rows[0].WorkDate);
        Assert.Equal(8m, result.Rows[0].Units);
        Assert.Equal("BASE", result.Rows[0].ComponentCode);
    }

    [Fact]
    public void Parse_OptionalComponentCodeColumn_IsAccepted()
    {
        var content = """
            workerRef,workDate,units
            W-001,2026-09-01,8
            """;

        var result = _parser.Parse(Stream(content));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
        Assert.Null(result.Rows[0].ComponentCode);
    }

    [Fact]
    public void Parse_MissingRequiredColumn_ReportsHeaderError()
    {
        var content = """
            workerRef,units
            W-001,8
            """;

        var result = _parser.Parse(Stream(content));

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, error => error.Code == "import.header_invalid");
    }

    [Fact]
    public void Parse_InvalidDateAndUnits_ReportRowErrors()
    {
        var content = """
            workerRef,workDate,units
            W-001,not-a-date,8
            W-002,2026-09-02,abc
            W-003,2026-09-03,-5
            """;

        var result = _parser.Parse(Stream(content));

        Assert.Equal(3, result.TotalDataRows);
        Assert.Empty(result.Rows);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Code == "import.date_invalid");
        Assert.Contains(result.Errors, error => error.Code == "import.units_invalid");
    }

    [Fact]
    public void Parse_QuotedFields_AreHandled()
    {
        var content = "workerRef,workDate,units,componentCode\n\"W,001\",2026-09-01,8,\"BASE\"\n";

        var result = _parser.Parse(Stream(content));

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("W,001", row.WorkerExternalRef);
        Assert.Equal("BASE", row.ComponentCode);
    }

    [Fact]
    public void Parse_SkipsBlankLines()
    {
        var content = "workerRef,workDate,units\n\nW-001,2026-09-01,8\n\n";

        var result = _parser.Parse(Stream(content));

        Assert.Equal(1, result.TotalDataRows);
        Assert.Single(result.Rows);
    }

    private static MemoryStream Stream(string content) => new(Encoding.UTF8.GetBytes(content));
}
