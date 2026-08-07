using ClosedXML.Excel;
using LedgerForge.ImportExport.Spreadsheets;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class LegacyBudgetWorkbookReaderTests
{
    [Fact]
    public void Read_RecalculatesTotalsAndPreservesSourceSequenceGaps()
    {
        using var stream = CreateWorkbook(
            [
                new TestBudgetRow(1, 2m, 10.50m, 999m, "Critical"),
                new TestBudgetRow(3, 1m, 5m, 5m, "High")
            ]);

        var expectation = new ImportReconciliationExpectation(2, 26m, "Critical", 1);
        var result = new LegacyBudgetWorkbookReader().Read(stream, "legacy-budget.xlsx", expectation);

        Assert.Equal(2, result.BudgetRows.Count);
        Assert.Equal(26m, result.Reconciliation.RecalculatedPlannedTotal);
        Assert.Equal(1, result.Reconciliation.PriorityNeedLevelCount);
        Assert.Equal(1, result.Reconciliation.PlannedTotalMismatchCount);
        Assert.False(result.Reconciliation.MatchesExpectation);
        Assert.Equal(64, result.SourceSha256.Length);
        Assert.Contains(result.Warnings, warning => warning.Contains("item 1", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("gap(s): 2", StringComparison.Ordinal));
        Assert.Equal(["Core"], result.LookupValues["Budget Section"]);
    }

    [Fact]
    public void Read_RejectsUnexpectedRawBudgetInfoHeader()
    {
        using var stream = CreateWorkbook([new TestBudgetRow(1, 1m, 10m, 10m, "Critical")], "Unexpected Item Header");

        var exception = Assert.Throws<LegacyBudgetWorkbookValidationException>(
            () => new LegacyBudgetWorkbookReader().Read(stream, "legacy-budget.xlsx"));

        Assert.Contains("Unexpected header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsDuplicateSourceItemNumbers()
    {
        using var stream = CreateWorkbook(
            [
                new TestBudgetRow(1, 1m, 10m, 10m, "Critical"),
                new TestBudgetRow(1, 1m, 20m, 20m, "High")
            ]);

        var exception = Assert.Throws<LegacyBudgetWorkbookValidationException>(
            () => new LegacyBudgetWorkbookReader().Read(stream, "legacy-budget.xlsx"));

        Assert.Contains("duplicate item number", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<TestBudgetRow> rows,
        string? firstRawHeaderOverride = null)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheetName in LegacyBudgetWorkbookSchema.RequiredSheets)
        {
            workbook.AddWorksheet(sheetName);
        }

        var raw = workbook.Worksheet(LegacyBudgetWorkbookSchema.RawBudgetInfoSheet);
        for (var column = 1; column <= LegacyBudgetWorkbookSchema.RawBudgetInfoHeaders.Count; column++)
        {
            raw.Cell(1, column).Value = column == 1 && firstRawHeaderOverride is not null
                ? firstRawHeaderOverride
                : LegacyBudgetWorkbookSchema.RawBudgetInfoHeaders[column - 1];
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 2;
            var source = rows[index];
            raw.Cell(rowNumber, 1).Value = source.ItemNumber;
            raw.Cell(rowNumber, 2).Value = $"Item {source.ItemNumber}";
            raw.Cell(rowNumber, 3).Value = "Replacement";
            raw.Cell(rowNumber, 5).Value = "Hardware";
            raw.Cell(rowNumber, 6).Value = "Technology";
            raw.Cell(rowNumber, 7).Value = "Headquarters";
            raw.Cell(rowNumber, 8).Value = source.Quantity;
            raw.Cell(rowNumber, 9).Value = source.UnitCost;
            raw.Cell(rowNumber, 10).Value = source.SourcePlannedTotal;
            raw.Cell(rowNumber, 11).Value = source.NeedLevel;
            raw.Cell(rowNumber, 13).Value = "Hardware";
            raw.Cell(rowNumber, 14).Value = "Annual";
            raw.Cell(rowNumber, 17).Value = "Ready";
            raw.Cell(rowNumber, 18).Value = false;
            raw.Cell(rowNumber, 19).Value = false;
            raw.Cell(rowNumber, 20).Value = false;
        }

        var lists = workbook.Worksheet(LegacyBudgetWorkbookSchema.ListsSheet);
        for (var column = 1; column <= LegacyBudgetWorkbookSchema.ListsHeaders.Count; column++)
        {
            lists.Cell(1, column).Value = LegacyBudgetWorkbookSchema.ListsHeaders[column - 1];
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
