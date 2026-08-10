using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Actuals;

public sealed record ActualImportProfile(
    string TransactionDateHeader,
    string AmountHeader,
    string DescriptionHeader,
    string SourceReferenceHeader,
    string BudgetItemHeader,
    string FinanceAccountHeader,
    string DepartmentHeader,
    string LocationHeader,
    string FiscalPeriodHeader)
{
    // FiscalPeriodHeader is retained so existing deployment configuration and callers
    // remain compatible. LedgerForge no longer reads or writes fiscal-period values.
    public static ActualImportProfile Default { get; } = new(
        "TransactionDate", "Amount", "Description", "SourceReference", "BudgetItem",
        "FinanceAccount", "Department", "Location", "FiscalPeriod");
}

public sealed record ActualImportResult(int ImportedRows, decimal ImportedTotal, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

public sealed class ActualCsvImportService(LedgerForgeDbContext dbContext)
{
    private const int MaximumRows = 100_000;

    public async Task<ActualImportResult> ImportAsync(
        Guid fiscalYearId,
        Stream source,
        ActualImportProfile profile,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (!source.CanRead) throw new ArgumentException("CSV stream is not readable.", nameof(source));

        using var reader = new StreamReader(source, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return new(0, 0m, ["CSV file is empty."]);
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        IReadOnlyList<IReadOnlyList<string>> records;
        try
        {
            records = ParseCsv(text);
        }
        catch (InvalidDataException exception)
        {
            return new(0, 0m, [exception.Message]);
        }

        if (records.Count < 2) return new(0, 0m, ["CSV must contain a header row and at least one data row."]);
        if (records.Count - 1 > MaximumRows) return new(0, 0m, [$"CSV exceeds the maximum of {MaximumRows:N0} data rows."]);

        var header = records[0]
            .Select((value, index) => new { Name = NormalizeHeader(value), Index = index })
            .Where(x => x.Name.Length > 0)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Index, StringComparer.OrdinalIgnoreCase);

        var required = new[] { profile.TransactionDateHeader, profile.AmountHeader, profile.DescriptionHeader };
        var missing = required.Where(x => !header.ContainsKey(NormalizeHeader(x))).ToArray();
        if (missing.Length > 0) return new(0, 0m, [$"Missing required CSV header(s): {string.Join(", ", missing)}."]);

        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken);
        if (year is null) throw new KeyNotFoundException("Fiscal year was not found.");
        if (year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            return new(0, 0m, [$"{year.DisplayName} is {year.Status} and cannot accept imported actuals."]);

        var latestVersionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var budgetItems = latestVersionId is null
            ? new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            : await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == latestVersionId.Value)
                .GroupBy(x => x.ItemNumber)
                .ToDictionaryAsync(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var accounts = await dbContext.FinanceAccounts.AsNoTracking().Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var departments = await dbContext.Departments.AsNoTracking().Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var locations = await dbContext.Locations.AsNoTracking().Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var pending = new List<ActualTransaction>(records.Count - 1);
        var errors = new List<string>();
        var seenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal total = 0m;
        var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(sourceFileName) ? "actuals.csv" : sourceFileName);
        _ = safeFileName;
        _ = profile.FiscalPeriodHeader;

        for (var recordIndex = 1; recordIndex < records.Count; recordIndex++)
        {
            var rowNumber = recordIndex + 1;
            var row = records[recordIndex];
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var dateText = Value(row, header, profile.TransactionDateHeader);
            var amountText = Value(row, header, profile.AmountHeader);
            var description = Value(row, header, profile.DescriptionHeader);
            if (!TryParseDate(dateText, out var transactionDate))
            {
                errors.Add($"Row {rowNumber}: TransactionDate '{dateText}' is invalid. Use yyyy-MM-dd.");
                continue;
            }
            if (transactionDate < year.StartDate || transactionDate > year.EndDate)
            {
                errors.Add($"Row {rowNumber}: transaction date {transactionDate:yyyy-MM-dd} is outside {year.DisplayName}.");
                continue;
            }
            if (!decimal.TryParse(amountText, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var amount) || amount <= 0m)
            {
                errors.Add($"Row {rowNumber}: Amount '{amountText}' must be a positive decimal using '.' as the decimal separator.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add($"Row {rowNumber}: Description is required.");
                continue;
            }

            Guid? budgetItemId = ResolveOptionalCode(row, header, profile.BudgetItemHeader, budgetItems, "budget item", rowNumber, errors);
            Guid? financeAccountId = ResolveOptionalCode(row, header, profile.FinanceAccountHeader, accounts, "finance account", rowNumber, errors);
            Guid? departmentId = ResolveOptionalCode(row, header, profile.DepartmentHeader, departments, "department", rowNumber, errors);
            Guid? locationId = ResolveOptionalCode(row, header, profile.LocationHeader, locations, "location", rowNumber, errors);

            if (errors.Any(x => x.StartsWith($"Row {rowNumber}:", StringComparison.Ordinal))) continue;

            var reference = Value(row, header, profile.SourceReferenceHeader);
            if (string.IsNullOrWhiteSpace(reference)) reference = $"import:{sourceHash}:row-{rowNumber}";

            if (!seenReferences.Add(reference))
            {
                errors.Add($"Row {rowNumber}: source reference '{reference}' is duplicated within this import file.");
                continue;
            }

            try
            {
                pending.Add(new ActualTransaction(
                    fiscalYearId,
                    transactionDate,
                    amount,
                    description,
                    ActualTransactionKind.Import,
                    reference,
                    budgetItemId,
                    financeAccountId,
                    departmentId,
                    locationId,
                    fiscalPeriodId: null));
                total = checked(total + amount);
            }
            catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or OverflowException)
            {
                errors.Add($"Row {rowNumber}: {exception.Message}");
            }
        }

        if (pending.Count == 0 && errors.Count == 0) errors.Add("CSV contains no non-empty data rows.");
        if (errors.Count > 0) return new(0, 0m, errors);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var importReferences = pending
            .Select(x => x.SourceReference)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingReferences = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.Kind == ActualTransactionKind.Import && x.SourceReference != null && importReferences.Contains(x.SourceReference))
            .Select(x => x.SourceReference!)
            .ToListAsync(cancellationToken);
        if (existingReferences.Count > 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var shown = string.Join(", ", existingReferences.Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
            return new(0, 0m, [$"Import rejected because previously posted import reference(s) were found: {shown}. No rows were posted."]);
        }

        dbContext.ActualTransactions.AddRange(pending);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(pending.Count, total, []);
    }

    private static Guid? ResolveOptionalCode(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> header,
        string configuredHeader,
        IReadOnlyDictionary<string, Guid> lookup,
        string label,
        int rowNumber,
        ICollection<string> errors)
    {
        var value = Value(row, header, configuredHeader);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (lookup.TryGetValue(value.Trim(), out var id)) return id;
        errors.Add($"Row {rowNumber}: {label} code '{value}' was not found or is inactive.");
        return null;
    }

    private static string Value(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> header, string configuredHeader)
    {
        if (!header.TryGetValue(NormalizeHeader(configuredHeader), out var index) || index >= row.Count) return string.Empty;
        return row[index].Trim();
    }

    private static string NormalizeHeader(string value) => value.Trim().Trim('\uFEFF');

    private static bool TryParseDate(string value, out DateOnly date)
        => DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    internal static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string text)
    {
        var records = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"')
            {
                if (field.Length != 0) throw new InvalidDataException("CSV contains a quote in the middle of an unquoted field.");
                quoted = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                records.Add(row.ToArray());
                row = [];
            }
            else
            {
                field.Append(ch);
            }
        }

        if (quoted) throw new InvalidDataException("CSV contains an unterminated quoted field.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            records.Add(row.ToArray());
        }

        return records;
    }
}