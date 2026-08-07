using System.Security.Cryptography;
using ClosedXML.Excel;

namespace LedgerForge.ImportExport.Spreadsheets;

public sealed class LegacyBudgetWorkbookReader
{
    private const decimal PlannedTotalTolerance = 0.005m;

    public LegacyBudgetWorkbookReadResult Read(
        Stream source,
        string sourceFileName,
        ImportReconciliationExpectation? expectation = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("The workbook stream must be readable.", nameof(source));
        if (string.IsNullOrWhiteSpace(sourceFileName)) throw new ArgumentException("The source file name is required.", nameof(sourceFileName));

        using var workbookBytes = CopyToMemory(source);
        var sourceHash = Convert.ToHexString(SHA256.HashData(workbookBytes.ToArray())).ToLowerInvariant();
        workbookBytes.Position = 0;

        using var workbook = new XLWorkbook(workbookBytes);
        ValidateRequiredSheets(workbook);

        var rawBudgetInfo = workbook.Worksheet(LegacyBudgetWorkbookSchema.RawBudgetInfoSheet);
        ValidateHeaders(rawBudgetInfo, LegacyBudgetWorkbookSchema.RawBudgetInfoHeaders);

        var lists = workbook.Worksheet(LegacyBudgetWorkbookSchema.ListsSheet);
        ValidateHeaders(lists, LegacyBudgetWorkbookSchema.ListsHeaders);

        var warnings = new List<string>();
        var budgetRows = ReadBudgetRows(rawBudgetInfo, warnings);
        var lookupValues = ReadLookupValues(lists);
        AddSequenceWarnings(budgetRows, warnings);

        var mismatchCount = budgetRows.Count(row => Math.Abs(row.RecalculatedPlannedTotal - row.SourcePlannedTotal) > PlannedTotalTolerance);
        var priorityNeedLevelCount = expectation?.PriorityNeedLevel is null
            ? 0
            : budgetRows.Count(row => string.Equals(row.NeedLevel, expectation.PriorityNeedLevel, StringComparison.OrdinalIgnoreCase));

        var reconciliation = new LegacyBudgetReconciliation(
            budgetRows.Count,
            budgetRows.Sum(row => row.RecalculatedPlannedTotal),
            mismatchCount,
            priorityNeedLevelCount,
            expectation);

        return new(
            sourceFileName.Trim(),
            sourceHash,
            budgetRows,
            lookupValues,
            reconciliation,
            warnings);
    }

    private static MemoryStream CopyToMemory(Stream source)
    {
        if (source.CanSeek) source.Position = 0;
        var memory = new MemoryStream();
        source.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    private static void ValidateRequiredSheets(XLWorkbook workbook)
    {
        var actualSheets = workbook.Worksheets.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var missing = LegacyBudgetWorkbookSchema.RequiredSheets.Where(x => !actualSheets.Contains(x)).ToArray();
        if (missing.Length > 0)
            throw new LegacyBudgetWorkbookValidationException($"Workbook is missing required sheet(s): {string.Join(", ", missing)}.");
    }

    private static void ValidateHeaders(IXLWorksheet worksheet, IReadOnlyList<string> expectedHeaders)
    {
        for (var column = 1; column <= expectedHeaders.Count; column++)
        {
            var actual = ReadOptionalText(worksheet.Cell(1, column)) ?? string.Empty;
            var expected = expectedHeaders[column - 1];
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new LegacyBudgetWorkbookValidationException($"Unexpected header on '{worksheet.Name}' at column {column}. Expected '{expected}' but found '{actual}'.");
        }
    }

    private static IReadOnlyList<LegacyBudgetRow> ReadBudgetRows(IXLWorksheet worksheet, ICollection<string> warnings)
    {
        var rows = new List<LegacyBudgetRow>();
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRowNumber; rowNumber++)
        {
            var itemCell = worksheet.Cell(rowNumber, 1);
            if (itemCell.IsEmpty()) continue;

            var itemNumber = ReadRequiredInt(itemCell, worksheet.Name, rowNumber, "Item");
            var quantity = ReadRequiredDecimal(worksheet.Cell(rowNumber, 8), worksheet.Name, rowNumber, "# of Units");
            var unitCost = ReadRequiredDecimal(worksheet.Cell(rowNumber, 9), worksheet.Name, rowNumber, "Estimated Cost / Unit");
            var sourcePlannedTotal = ReadRequiredDecimal(worksheet.Cell(rowNumber, 10), worksheet.Name, rowNumber, "Estimated Cost");

            if (quantity < 0) throw new LegacyBudgetWorkbookValidationException($"'{worksheet.Name}' row {rowNumber} has a negative quantity.");
            if (unitCost < 0) throw new LegacyBudgetWorkbookValidationException($"'{worksheet.Name}' row {rowNumber} has a negative unit cost.");

            var row = new LegacyBudgetRow(
                rowNumber,
                itemNumber,
                ReadRequiredText(worksheet.Cell(rowNumber, 2), worksheet.Name, rowNumber, "Item Description"),
                ReadRequiredText(worksheet.Cell(rowNumber, 3), worksheet.Name, rowNumber, "Reason / Purpose"),
                ReadOptionalDate(worksheet.Cell(rowNumber, 4), worksheet.Name, rowNumber, "Estimated Month"),
                ReadRequiredText(worksheet.Cell(rowNumber, 5), worksheet.Name, rowNumber, "Type"),
                ReadRequiredText(worksheet.Cell(rowNumber, 6), worksheet.Name, rowNumber, "Department"),
                ReadRequiredText(worksheet.Cell(rowNumber, 7), worksheet.Name, rowNumber, "Location"),
                quantity,
                unitCost,
                sourcePlannedTotal,
                ReadRequiredText(worksheet.Cell(rowNumber, 11), worksheet.Name, rowNumber, "Need Level"),
                ReadOptionalText(worksheet.Cell(rowNumber, 12)),
                ReadRequiredText(worksheet.Cell(rowNumber, 13), worksheet.Name, rowNumber, "Internal Category"),
                ReadRequiredText(worksheet.Cell(rowNumber, 14), worksheet.Name, rowNumber, "Frequency"),
                ReadOptionalDate(worksheet.Cell(rowNumber, 15), worksheet.Name, rowNumber, "Renewal Date"),
                ReadOptionalText(worksheet.Cell(rowNumber, 16)),
                ReadRequiredText(worksheet.Cell(rowNumber, 17), worksheet.Name, rowNumber, "Status"),
                ReadRequiredBoolean(worksheet.Cell(rowNumber, 18), worksheet.Name, rowNumber, "Approval"),
                ReadRequiredBoolean(worksheet.Cell(rowNumber, 19), worksheet.Name, rowNumber, "Denial"),
                ReadRequiredBoolean(worksheet.Cell(rowNumber, 20), worksheet.Name, rowNumber, "Proposed"));

            if (Math.Abs(row.RecalculatedPlannedTotal - row.SourcePlannedTotal) > PlannedTotalTolerance)
                warnings.Add($"Raw Budget Info row {rowNumber} item {itemNumber} has source total {row.SourcePlannedTotal:0.####} but recalculates to {row.RecalculatedPlannedTotal:0.####}.");

            rows.Add(row);
        }

        var duplicateItemNumbers = rows.GroupBy(x => x.ItemNumber).Where(x => x.Count() > 1).Select(x => x.Key).OrderBy(x => x).ToArray();
        if (duplicateItemNumbers.Length > 0)
            throw new LegacyBudgetWorkbookValidationException($"Raw Budget Info contains duplicate item number(s): {string.Join(", ", duplicateItemNumbers)}.");

        return rows;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadLookupValues(IXLWorksheet worksheet)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var column = 1; column <= LegacyBudgetWorkbookSchema.ListsHeaders.Count; column++)
        {
            var header = LegacyBudgetWorkbookSchema.ListsHeaders[column - 1];
            var values = new List<string>();
            for (var rowNumber = 2; rowNumber <= lastRowNumber; rowNumber++)
            {
                var value = ReadOptionalText(worksheet.Cell(rowNumber, column));
                if (value is not null) values.Add(value);
            }
            result.Add(header, values);
        }
        return result;
    }

    private static void AddSequenceWarnings(IReadOnlyCollection<LegacyBudgetRow> rows, ICollection<string> warnings)
    {
        if (rows.Count == 0) return;
        var itemNumbers = rows.Select(x => x.ItemNumber).ToHashSet();
        var minimum = itemNumbers.Min();
        var maximum = itemNumbers.Max();
        var gaps = Enumerable.Range(minimum, maximum - minimum + 1).Where(x => !itemNumbers.Contains(x)).ToArray();
        if (gaps.Length > 0)
            warnings.Add($"Source item numbering contains gap(s): {string.Join(", ", gaps)}. Source item numbers should be preserved unless the import profile explicitly remaps them.");
    }

    private static string ReadRequiredText(IXLCell cell, string sheetName, int rowNumber, string fieldName)
        => ReadOptionalText(cell) ?? throw new LegacyBudgetWorkbookValidationException($"'{sheetName}' row {rowNumber} is missing required field '{fieldName}'.");

    private static string? ReadOptionalText(IXLCell cell)
    {
        if (cell.IsEmpty() || !cell.TryGetValue<string>(out var value)) return null;
        value = value.Trim();
        return value.Length == 0 ? null : value;
    }

    private static int ReadRequiredInt(IXLCell cell, string sheetName, int rowNumber, string fieldName)
    {
        if (!cell.TryGetValue<int>(out var value)) throw new LegacyBudgetWorkbookValidationException($"'{sheetName}' row {rowNumber} field '{fieldName}' must be an integer.");
        return value;
    }

    private static decimal ReadRequiredDecimal(IXLCell cell, string sheetName, int rowNumber, string fieldName)
    {
        if (!cell.TryGetValue<decimal>(out var value)) throw new LegacyBudgetWorkbookValidationException($"'{sheetName}' row {rowNumber} field '{fieldName}' must be numeric.");
        return value;
    }

    private static bool ReadRequiredBoolean(IXLCell cell, string sheetName, int rowNumber, string fieldName)
    {
        if (!cell.TryGetValue<bool>(out var value)) throw new LegacyBudgetWorkbookValidationException($"'{sheetName}' row {rowNumber} field '{fieldName}' must be TRUE or FALSE.");
        return value;
    }

    private static DateOnly? ReadOptionalDate(IXLCell cell, string sheetName, int rowNumber, string fieldName)
    {
        if (cell.IsEmpty()) return null;
        if (!cell.TryGetValue<DateTime>(out var dateTime)) throw new LegacyBudgetWorkbookValidationException($"'{sheetName}' row {rowNumber} field '{fieldName}' must be a valid Excel date.");
        return DateOnly.FromDateTime(dateTime);
    }
}
