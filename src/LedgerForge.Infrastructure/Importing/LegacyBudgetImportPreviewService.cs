using System.Security.Cryptography;
using System.Text.Json;
using LedgerForge.Domain.Importing;
using LedgerForge.Domain.MasterData;
using LedgerForge.ImportExport.Spreadsheets;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Importing;

public sealed record LegacyBudgetImportPreviewResult(
    Guid ImportBatchId,
    ImportBatchStatus Status,
    int SourceRowCount,
    int AcceptedRowCount,
    int RejectedRowCount,
    int WarningCount,
    int ErrorCount,
    bool ReconciledToExpectedTargets);

public sealed class LegacyBudgetImportPreviewService(
    LedgerForgeDbContext dbContext,
    LegacyBudgetWorkbookReader workbookReader)
{
    private const string ImportType = "LEGACY_BUDGET_WORKBOOK";
    private const decimal PlannedTotalTolerance = 0.005m;

    public async Task<LegacyBudgetImportPreviewResult> CreatePreviewAsync(
        Stream source,
        string sourceFileName,
        ImportReconciliationExpectation? expectation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("The workbook stream must be readable.", nameof(source));
        if (string.IsNullOrWhiteSpace(sourceFileName)) throw new ArgumentException("Source filename is required.", nameof(sourceFileName));

        using var buffer = new MemoryStream();
        if (source.CanSeek) source.Position = 0;
        await source.CopyToAsync(buffer, cancellationToken);
        var sourceHash = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();

        var batch = new ImportBatch(ImportType, sourceFileName, sourceHash, buffer.Length);
        batch.BeginValidation();
        dbContext.ImportBatches.Add(batch);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            buffer.Position = 0;
            var workbook = workbookReader.Read(buffer, sourceFileName, expectation);
            if (!string.Equals(workbook.SourceSha256, sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Workbook hash changed while generating the import preview.");

            var lookups = await LookupSnapshot.LoadAsync(dbContext, cancellationToken);
            var aliases = await dbContext.LookupValueAliases.Where(x => x.IsActive).ToListAsync(cancellationToken);

            var warningCount = 0;
            var errorCount = 0;
            var acceptedRowCount = 0;
            var rejectedRowCount = 0;
            var exceptionCount = 0;

            foreach (var sourceRow in workbook.BudgetRows)
            {
                var importRow = new ImportRow(
                    batch.Id,
                    LegacyBudgetWorkbookSchema.RawBudgetInfoSheet,
                    sourceRow.SourceRowNumber,
                    $"LEGACY:{sourceRow.ItemNumber}",
                    JsonSerializer.Serialize(sourceRow));
                dbContext.ImportRows.Add(importRow);

                var rowIssues = ValidateRow(sourceRow, importRow.Id, batch.Id, lookups, aliases);
                foreach (var issue in rowIssues)
                {
                    dbContext.ImportExceptions.Add(issue);
                    exceptionCount++;
                    if (issue.Severity == ImportExceptionSeverity.Error) errorCount++;
                    else warningCount++;
                }

                if (rowIssues.Any(x => x.Severity == ImportExceptionSeverity.Error))
                {
                    importRow.Reject();
                    rejectedRowCount++;
                }
                else
                {
                    importRow.Accept(rowIssues.Count > 0);
                    acceptedRowCount++;
                }
            }

            foreach (var warning in workbook.Warnings)
            {
                dbContext.ImportExceptions.Add(new ImportException(batch.Id, null, "WORKBOOK_WARNING", warning, ImportExceptionSeverity.Warning));
                warningCount++;
                exceptionCount++;
            }

            if (!workbook.Reconciliation.MatchesExpectation)
            {
                dbContext.ImportExceptions.Add(new ImportException(
                    batch.Id,
                    null,
                    "RECONCILIATION_EXPECTATION_MISMATCH",
                    "The workbook does not match the configured import reconciliation expectation. Explicit administrator acceptance with a reason is required before commit.",
                    ImportExceptionSeverity.Warning));
                warningCount++;
                exceptionCount++;
            }

            batch.CompletePreview(
                workbook.BudgetRows.Count,
                acceptedRowCount,
                exceptionCount,
                workbook.Reconciliation.RecalculatedPlannedTotal,
                workbook.Reconciliation.PriorityNeedLevelCount,
                workbook.Reconciliation.MatchesExpectation,
                DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(batch.Id, batch.Status, workbook.BudgetRows.Count, acceptedRowCount, rejectedRowCount, warningCount, errorCount, workbook.Reconciliation.MatchesExpectation);
        }
        catch (LegacyBudgetWorkbookValidationException exception)
        {
            batch.MarkValidationFailed();
            dbContext.ImportExceptions.Add(new ImportException(batch.Id, null, "WORKBOOK_VALIDATION_FAILED", exception.Message, ImportExceptionSeverity.Error));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(batch.Id, batch.Status, 0, 0, 0, 0, 1, false);
        }
    }

    private static IReadOnlyList<ImportException> ValidateRow(
        LegacyBudgetRow row,
        Guid importRowId,
        Guid importBatchId,
        LookupSnapshot lookups,
        IReadOnlyCollection<LookupValueAlias> aliases)
    {
        var issues = new List<ImportException>();
        ValidateLookup("FinanceType", row.FinanceType, lookups.FinanceTypes, false, aliases, importBatchId, importRowId, issues);
        ValidateLookup("Department", row.Department, lookups.Departments, true, aliases, importBatchId, importRowId, issues);
        ValidateLookup("Location", row.Location, lookups.Locations, true, aliases, importBatchId, importRowId, issues);
        ValidateLookup("NeedLevel", row.NeedLevel, lookups.NeedLevels, true, aliases, importBatchId, importRowId, issues);
        ValidateLookup("InternalCategory", row.InternalCategory, lookups.InternalCategories, false, aliases, importBatchId, importRowId, issues);
        ValidateLookup("Frequency", row.Frequency, lookups.Frequencies, false, aliases, importBatchId, importRowId, issues);

        var workflowFlags = (row.Approval ? 1 : 0) + (row.Denial ? 1 : 0) + (row.Proposed ? 1 : 0);
        if (workflowFlags > 1)
            issues.Add(new ImportException(importBatchId, importRowId, "CONTRADICTORY_WORKFLOW_FLAGS", $"Item {row.ItemNumber} has more than one workflow decision flag set.", ImportExceptionSeverity.Error));

        if (Math.Abs(row.RecalculatedPlannedTotal - row.SourcePlannedTotal) > PlannedTotalTolerance)
            issues.Add(new ImportException(importBatchId, importRowId, "SOURCE_TOTAL_MISMATCH", $"Item {row.ItemNumber} source total {row.SourcePlannedTotal:0.####} recalculates to {row.RecalculatedPlannedTotal:0.####}; the recalculated value will be authoritative.", ImportExceptionSeverity.Warning));

        if (string.IsNullOrWhiteSpace(row.Vendor))
            issues.Add(new ImportException(importBatchId, importRowId, "MISSING_VENDOR", $"Item {row.ItemNumber} does not identify a vendor. Vendor assignment should be reviewed.", ImportExceptionSeverity.Warning));

        return issues;
    }

    private static void ValidateLookup(
        string lookupType,
        string sourceValue,
        LookupIndex index,
        bool parseLeadingCode,
        IReadOnlyCollection<LookupValueAlias> aliases,
        Guid importBatchId,
        Guid importRowId,
        ICollection<ImportException> issues)
    {
        if (TryResolveLookup(lookupType, sourceValue, index, parseLeadingCode, aliases, out _)) return;
        issues.Add(new ImportException(importBatchId, importRowId, $"UNKNOWN_{lookupType.ToUpperInvariant()}", $"Source value '{sourceValue}' does not map to an active {lookupType} lookup value.", ImportExceptionSeverity.Error));
    }

    private static bool TryResolveLookup(
        string lookupType,
        string sourceValue,
        LookupIndex index,
        bool parseLeadingCode,
        IReadOnlyCollection<LookupValueAlias> aliases,
        out string? canonicalCode)
    {
        var alias = aliases.FirstOrDefault(x => string.Equals(x.LookupType, lookupType, StringComparison.OrdinalIgnoreCase) && string.Equals(x.SourceValue, sourceValue, StringComparison.OrdinalIgnoreCase));
        if (alias is not null && index.Codes.Contains(alias.CanonicalCode))
        {
            canonicalCode = alias.CanonicalCode;
            return true;
        }

        if (parseLeadingCode)
        {
            var separatorIndex = sourceValue.IndexOf('=');
            var sourceCode = (separatorIndex >= 0 ? sourceValue[..separatorIndex] : sourceValue).Trim();
            if (index.Codes.Contains(sourceCode))
            {
                canonicalCode = sourceCode;
                return true;
            }
        }

        if (index.Names.TryGetValue(sourceValue.Trim(), out canonicalCode)) return true;
        canonicalCode = null;
        return false;
    }

    private sealed record LookupIndex(HashSet<string> Codes, Dictionary<string, string> Names)
    {
        public static LookupIndex Create(IEnumerable<(string Code, string Name)> values)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                codes.Add(value.Code);
                names.TryAdd(value.Name, value.Code);
            }
            return new(codes, names);
        }
    }

    private sealed record LookupSnapshot(
        LookupIndex FinanceTypes,
        LookupIndex Departments,
        LookupIndex Locations,
        LookupIndex NeedLevels,
        LookupIndex InternalCategories,
        LookupIndex Frequencies)
    {
        public static async Task<LookupSnapshot> LoadAsync(LedgerForgeDbContext dbContext, CancellationToken cancellationToken)
        {
            var financeTypes = await dbContext.FinanceTypes.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
            var departments = await dbContext.Departments.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
            var locations = await dbContext.Locations.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
            var needLevels = await dbContext.NeedLevels.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
            var internalCategories = await dbContext.InternalCategories.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);
            var frequencies = await dbContext.Frequencies.Where(x => x.IsActive).Select(x => new { x.Code, x.Name }).ToListAsync(cancellationToken);

            return new(
                LookupIndex.Create(financeTypes.Select(x => (x.Code, x.Name))),
                LookupIndex.Create(departments.Select(x => (x.Code, x.Name))),
                LookupIndex.Create(locations.Select(x => (x.Code, x.Name))),
                LookupIndex.Create(needLevels.Select(x => (x.Code, x.Name))),
                LookupIndex.Create(internalCategories.Select(x => (x.Code, x.Name))),
                LookupIndex.Create(frequencies.Select(x => (x.Code, x.Name))));
        }
    }
}
