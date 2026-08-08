using System.Text.Json;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Importing;
using LedgerForge.Domain.MasterData;
using LedgerForge.ImportExport.Spreadsheets;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Importing;

public sealed record ImportCommitTarget(
    Guid FiscalYearId,
    string FiscalYearName,
    Guid BudgetVersionId,
    string BudgetVersionName,
    int VersionNumber);

public sealed record ImportCommitResult(
    Guid ImportBatchId,
    Guid FiscalYearId,
    Guid BudgetVersionId,
    int CommittedRowCount,
    decimal CommittedPlannedTotal);

public sealed class LegacyBudgetImportCommitService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<ImportCommitTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BudgetVersions
            .AsNoTracking()
            .Where(version => !version.IsLocked)
            .Join(
                dbContext.FiscalYears.AsNoTracking().Where(year =>
                    year.LockedAtUtc == null &&
                    year.Status != FiscalYearStatus.Closed &&
                    year.Status != FiscalYearStatus.Archived),
                version => version.FiscalYearId,
                year => year.Id,
                (version, year) => new ImportCommitTarget(
                    year.Id,
                    year.DisplayName,
                    version.Id,
                    version.Name,
                    version.VersionNumber))
            .OrderByDescending(x => x.FiscalYearName)
            .ThenByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<ImportCommitResult> CommitAsync(
        Guid batchId,
        Guid budgetVersionId,
        CancellationToken cancellationToken = default)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Import batch ID is required.", nameof(batchId));
        if (budgetVersionId == Guid.Empty) throw new ArgumentException("Budget version ID is required.", nameof(budgetVersionId));

        var batch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Import batch was not found.");

        if (batch.Status != ImportBatchStatus.PreviewReady || string.IsNullOrWhiteSpace(batch.AcceptedBy))
            throw new InvalidOperationException("Import preview must be explicitly accepted before authoritative commit.");

        var version = await dbContext.BudgetVersions
            .SingleOrDefaultAsync(x => x.Id == budgetVersionId, cancellationToken)
            ?? throw new KeyNotFoundException("Target budget version was not found.");
        if (version.IsLocked)
            throw new InvalidOperationException("The selected budget version is locked and cannot receive imported items.");

        var fiscalYear = await dbContext.FiscalYears
            .SingleAsync(x => x.Id == version.FiscalYearId, cancellationToken);
        if (fiscalYear.LockedAtUtc is not null || fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("The selected fiscal year is locked, closed, or archived.");

        var importRows = await dbContext.ImportRows
            .Where(x => x.ImportBatchId == batchId &&
                        (x.Outcome == ImportRowOutcome.Accepted || x.Outcome == ImportRowOutcome.AcceptedWithWarning))
            .OrderBy(x => x.SourceRowNumber)
            .ToListAsync(cancellationToken);
        if (importRows.Count == 0)
            throw new InvalidOperationException("The accepted import preview contains no rows eligible for commit.");

        var sourceRows = importRows
            .Select(row => new ParsedRow(
                row,
                JsonSerializer.Deserialize<LegacyBudgetRow>(row.RawDataJson)
                    ?? throw new InvalidOperationException($"Import row {row.SourceRowNumber} could not be deserialized.")))
            .ToArray();

        var duplicateSourceNumbers = sourceRows
            .GroupBy(x => x.Source.ItemNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateSourceNumbers.Length > 0)
            throw new InvalidOperationException($"Accepted import rows contain duplicate item numbers: {string.Join(", ", duplicateSourceNumbers)}.");

        var itemNumbers = sourceRows
            .Select(x => x.Source.ItemNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var targetCollisions = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id &&
                        x.BudgetVersionId == version.Id &&
                        itemNumbers.Contains(x.ItemNumber))
            .Select(x => x.ItemNumber)
            .ToListAsync(cancellationToken);
        if (targetCollisions.Count > 0)
            throw new InvalidOperationException($"The target budget version already contains item number(s): {string.Join(", ", targetCollisions.Order())}.");

        var lookups = await CommitLookupSnapshot.LoadAsync(dbContext, cancellationToken);
        var aliases = await dbContext.LookupValueAliases
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var resolvedRows = sourceRows
            .Select(row => Resolve(row, lookups, aliases))
            .ToArray();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var committedTotal = 0m;
            foreach (var resolved in resolvedRows)
            {
                var source = resolved.Parsed.Source;
                var itemNumber = source.ItemNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var item = new BudgetItem(
                    fiscalYear.Id,
                    version.Id,
                    $"IMP-{batch.Id:N}-{source.ItemNumber}",
                    itemNumber,
                    source.ItemDescription,
                    source.Quantity,
                    source.UnitCost);

                item.UpdatePlanningDetails(
                    itemNumber,
                    source.ItemDescription,
                    source.ReasonPurpose,
                    source.RenewalDate is null ? PurchaseType.New : PurchaseType.Renewal,
                    source.EstimatedMonth,
                    source.RenewalDate,
                    null,
                    resolved.FinanceTypeId,
                    resolved.DepartmentId,
                    resolved.LocationId,
                    resolved.NeedLevelId,
                    resolved.InternalCategoryId,
                    resolved.FrequencyId);

                if (source.Denial)
                {
                    item.SetStatus(BudgetItemStatus.Denied);
                }
                else if (source.Approval)
                {
                    item.SetStatus(BudgetItemStatus.Approved);
                    item.SetApprovedTotal(source.RecalculatedPlannedTotal);
                }
                else if (source.Proposed)
                {
                    item.SetStatus(BudgetItemStatus.Proposed);
                }

                dbContext.BudgetItems.Add(item);
                resolved.Parsed.ImportRow.MarkCommitted(nameof(BudgetItem), item.Id);
                committedTotal = checked(committedTotal + item.PlannedTotal);
            }

            batch.MarkCommitted(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new(
                batch.Id,
                fiscalYear.Id,
                version.Id,
                resolvedRows.Length,
                committedTotal);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static ResolvedRow Resolve(
        ParsedRow parsed,
        CommitLookupSnapshot lookups,
        IReadOnlyCollection<LookupValueAlias> aliases)
    {
        var source = parsed.Source;
        return new(
            parsed,
            ResolveRequired("FinanceType", source.FinanceType, lookups.FinanceTypes, false, aliases),
            ResolveRequired("Department", source.Department, lookups.Departments, true, aliases),
            ResolveRequired("Location", source.Location, lookups.Locations, true, aliases),
            ResolveRequired("NeedLevel", source.NeedLevel, lookups.NeedLevels, true, aliases),
            ResolveRequired("InternalCategory", source.InternalCategory, lookups.InternalCategories, false, aliases),
            ResolveRequired("Frequency", source.Frequency, lookups.Frequencies, false, aliases));
    }

    private static Guid ResolveRequired(
        string lookupType,
        string sourceValue,
        CommitLookupIndex index,
        bool parseLeadingCode,
        IReadOnlyCollection<LookupValueAlias> aliases)
    {
        var alias = aliases.FirstOrDefault(x =>
            string.Equals(x.LookupType, lookupType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SourceValue, sourceValue, StringComparison.OrdinalIgnoreCase));
        if (alias is not null && index.Codes.TryGetValue(alias.CanonicalCode, out var aliasId))
            return aliasId;

        if (parseLeadingCode)
        {
            var separatorIndex = sourceValue.IndexOf('=');
            var sourceCode = (separatorIndex >= 0 ? sourceValue[..separatorIndex] : sourceValue).Trim();
            if (index.Codes.TryGetValue(sourceCode, out var codeId))
                return codeId;
        }

        if (index.Names.TryGetValue(sourceValue.Trim(), out var nameId))
            return nameId;

        throw new InvalidOperationException($"Source value '{sourceValue}' no longer maps to an active {lookupType} lookup value. Refresh the preview or restore the mapping before commit.");
    }

    private sealed record ParsedRow(ImportRow ImportRow, LegacyBudgetRow Source);

    private sealed record ResolvedRow(
        ParsedRow Parsed,
        Guid FinanceTypeId,
        Guid DepartmentId,
        Guid LocationId,
        Guid NeedLevelId,
        Guid InternalCategoryId,
        Guid FrequencyId);

    private sealed record CommitLookupIndex(
        Dictionary<string, Guid> Codes,
        Dictionary<string, Guid> Names)
    {
        public static CommitLookupIndex Create(IEnumerable<(Guid Id, string Code, string Name)> values)
        {
            var codes = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var names = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values)
            {
                codes.TryAdd(value.Code, value.Id);
                names.TryAdd(value.Name, value.Id);
            }
            return new(codes, names);
        }
    }

    private sealed record CommitLookupSnapshot(
        CommitLookupIndex FinanceTypes,
        CommitLookupIndex Departments,
        CommitLookupIndex Locations,
        CommitLookupIndex NeedLevels,
        CommitLookupIndex InternalCategories,
        CommitLookupIndex Frequencies)
    {
        public static async Task<CommitLookupSnapshot> LoadAsync(
            LedgerForgeDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var financeTypes = await dbContext.FinanceTypes.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);
            var departments = await dbContext.Departments.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);
            var locations = await dbContext.Locations.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);
            var needLevels = await dbContext.NeedLevels.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);
            var categories = await dbContext.InternalCategories.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);
            var frequencies = await dbContext.Frequencies.Where(x => x.IsActive).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(cancellationToken);

            return new(
                CommitLookupIndex.Create(financeTypes.Select(x => (x.Id, x.Code, x.Name))),
                CommitLookupIndex.Create(departments.Select(x => (x.Id, x.Code, x.Name))),
                CommitLookupIndex.Create(locations.Select(x => (x.Id, x.Code, x.Name))),
                CommitLookupIndex.Create(needLevels.Select(x => (x.Id, x.Code, x.Name))),
                CommitLookupIndex.Create(categories.Select(x => (x.Id, x.Code, x.Name))),
                CommitLookupIndex.Create(frequencies.Select(x => (x.Id, x.Code, x.Name))));
        }
    }
}
