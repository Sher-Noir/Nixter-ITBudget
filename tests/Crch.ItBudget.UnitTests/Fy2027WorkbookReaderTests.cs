using ClosedXML.Excel;
using Crch.ItBudget.ImportExport.Fy2027;

namespace Crch.ItBudget.UnitTests;

public sealed class Fy2027WorkbookReaderTests
{
    [Fact]
    public void Read_RecalculatesTotalsAndPreservesSourceSequenceGaps()
    {
        using var stream = CreateWorkbook(
            [
                new TestBudgetRow(1, 2m, 10.50m, 999m, "4 = Must Have"),
                new TestBudgetRow(3, 1m, 5m, 5m, "3 = High")
            ]);

        var result = new Fy2027WorkbookReader().Read(stream, "fy2027.xlsx");

        Assert.Equal(2, result.BudgetRows.Count);
        Assert.Equal(26m, result.Reconciliation.RecalculatedPlannedTotal);
        Assert.Equal(1, result.Reconciliation.MustHaveCount);
        Assert.Equal(1, result.Reconciliation.PlannedTotalMismatchCount);
        Assert.False(result.Reconciliation.MeetsTargets);
        Assert.Equal(64, result.SourceSha256.Length);
        Assert.Contains(result.Warnings, warning => warning.Contains("item 1", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("gap(s): 2", StringComparison.Ordinal));
        Assert.Equal(["Core"], result.LookupValues["Budget Section"]);
    }

    [Fact]
    public void Read_RejectsUnexpectedRawBudgetInfoHeader()
    {
        using var stream = CreateWorkbook([new TestBudgetRow(1, 1m, 10m, 10m, "4 = Must Have")], "Unexpected Item Header");

        var exception = Assert.Throws<Fy2027WorkbookValidationException>(
            () => new Fy2027WorkbookReader().Read(stream, "fy2027.xlsx"));

        Assert.Contains("Unexpected header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsDuplicateSourceItemNumbers()
    {
        using var stream = CreateWorkbook(
            [
                new TestBudgetRow(1, 1m, 10m, 10m, "4 = Must Have"),
                new TestBudgetRow(1, 1m, 20m, 20m, "3 = High")
            ]);

        var exception = Assert.Throws<Fy2027WorkbookValidationException>(
            () => new Fy2027WorkbookReader().Read(stream, "fy2027.xlsx"));

        Assert.Contains("duplicate item number", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<TestBudgetRow> rows,
        string? firstRawHeaderOverride = null)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheetName in Fy2027WorkbookSchema.RequiredSheets)
        {
            workbook.AddWorksheet(sheetName);
        }

        var raw = workbook.Worksheet(Fy2027WorkbookSchema.RawBudgetInfoSheet);
        for (var column = 1; column <= Fy2027WorkbookSchema.RawBudgetInfoHeaders.Count; column++)
        {
            raw.Cell(1, column).Value = column == 1 && firstRawHeaderOverride is not null
                ? firstRawHeaderOverride
                : Fy2027WorkbookSchema.RawBudgetInfoHeaders[column - 1];
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 2;
            var source = rows[index];
            raw.Cell(rowNumber, 1).Value = source.ItemNumber;
            raw.Cell(rowNumber, 2).Value = $"Item {source.ItemNumber}";
            raw.Cell(rowNumber, 3).Value = "Replacement";
            raw.Cell(rowNumber, 5).Value = "Comp. Hardware";
            raw.Cell(rowNumber, 6).Value = "17 = IT/IS";
            raw.Cell(rowNumber, 7).Value = "ALL = All Locations";
            raw.Cell(rowNumber, 8).Value = source.Quantity;
            raw.Cell(rowNumber, 9).Value = source.UnitCost;
            raw.Cell(rowNumber, 10).Value = source.SourcePlannedTotal;
            raw.Cell(rowNumber, 11).Value = source.NeedLevel;
            raw.Cell(rowNumber, 13).Value = "Hardware";
            raw.Cell(rowNumber, 14).Value = "Annually";
            raw.Cell(rowNumber, 17).Value = "Ready";
            raw.Cell(rowNumber, 18).Value = false;
            raw.Cell(rowNumber, 19).Value = false;
            raw.Cell(rowNumber, 20).Value = false;
        }

        var lists = workbook.Worksheet(Fy2027WorkbookSchema.ListsSheet);
        for (var column = 1; column <= Fy2027WorkbookSchema.ListsHeaders.Count; column++)
        {
            lists.Cell(1, column).Value = Fy2027WorkbookSchema.ListsHeaders[column - 1];
        }

        lists.Cell(2, 1).Value = "Core";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private sealed record TestBudgetRow(
        int ItemNumber,
        decimal Quantity,
        decimal UnitCost,
        decimal SourcePlannedTotal,
        string NeedLevel);
}
