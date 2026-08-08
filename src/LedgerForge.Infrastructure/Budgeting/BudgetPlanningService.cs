using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetItemSummary(
    Guid Id,
    string StableIdentifier,
    string ItemNumber,
    string Description,
    string? ReasonPurpose,
    PurchaseType PurchaseType,
    decimal Quantity,
    decimal UnitCost,
    decimal PlannedTotal,
    decimal? ApprovedTotal,
    decimal? RevisedTotal,
    DateOnly? EstimatedPurchaseDate,
    DateOnly? RenewalDate,
    BudgetItemStatus Status);

public sealed record BudgetPlanningSnapshot(
    IReadOnlyList<FiscalYearSummary> FiscalYears,
    Guid? SelectedFiscalYearId,
    IReadOnlyList<BudgetVersionSummary> Versions,
    Guid? SelectedVersionId,
    bool SelectedVersionLocked,
    IReadOnlyList<BudgetItemSummary> Items,
    decimal PlannedTotal,
    decimal ApprovedTotal,
    decimal RevisedTotal);

public sealed record LookupOption(Guid Id, string Code, string Name);

public sealed record BudgetItemEditSnapshot(
    Guid Id,
    Guid FiscalYearId,
    string FiscalYearName,
    Guid BudgetVersionId,
    string BudgetVersionName,
    bool VersionLocked,
    string StableIdentifier,
    string ItemNumber,
    string Description,
    string? ReasonPurpose,
    PurchaseType PurchaseType,
    Guid? BudgetSectionId,
    Guid? FinanceTypeId,
    Guid? DepartmentId,
    Guid? LocationId,
    Guid? NeedLevelId,
    Guid? InternalCategoryId,
    Guid? FrequencyId,
    decimal Quantity,
    decimal UnitCost,
    decimal PlannedTotal,
    decimal? ApprovedTotal,
    decimal? RevisedTotal,
    DateOnly? EstimatedPurchaseDate,
    DateOnly? RenewalDate,
    BudgetItemStatus Status,
    IReadOnlyList<LookupOption> BudgetSections,
    IReadOnlyList<LookupOption> FinanceTypes,
    IReadOnlyList<LookupOption> Departments,
    IReadOnlyList<LookupOption> Locations,
    IReadOnlyList<LookupOption> NeedLevels,
    IReadOnlyList<LookupOption> InternalCategories,
    IReadOnlyList<LookupOption> Frequencies);

public sealed class BudgetPlanningService(
    LedgerForgeDbContext dbContext,
    FiscalYearAdministrationService fiscalYearService)
{
    public async Task<BudgetPlanningSnapshot> GetSnapshotAsync(
        Guid? fiscalYearId,
        Guid? versionId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYears = await fiscalYearService.ListAsync(cancellationToken);
        var selectedFiscalYearId = ResolveFiscalYear(fiscalYears, fiscalYearId);
        if (selectedFiscalYearId is null)
            return new(fiscalYears, null, [], null, false, [], 0m, 0m, 0m);

        var versions = await fiscalYearService.ListVersionsAsync(selectedFiscalYearId.Value, cancellationToken);
        var selectedVersion = ResolveVersion(versions, versionId);
        if (selectedVersion is null)
            return new(fiscalYears, selectedFiscalYearId, versions, null, false, [], 0m, 0m, 0m);

        var items = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.FiscalYearId == selectedFiscalYearId && x.BudgetVersionId == selectedVersion.Id)
            .OrderBy(x => x.ItemNumber)
            .ThenBy(x => x.Description)
            .Select(x => new BudgetItemSummary(
                x.Id,
                x.StableIdentifier,
                x.ItemNumber,
                x.Description,
                x.ReasonPurpose,
                x.PurchaseType,
                x.Quantity,
                x.UnitCost,
                x.PlannedTotal,
                x.ApprovedTotal,
                x.RevisedTotal,
                x.EstimatedPurchaseDate,
                x.RenewalDate,
                x.Status))
            .ToListAsync(cancellationToken);

        return new(
            fiscalYears,
            selectedFiscalYearId,
            versions,
            selectedVersion.Id,
            selectedVersion.IsLocked,
            items,
            items.Sum(x => x.PlannedTotal),
            items.Sum(x => x.ApprovedTotal ?? 0m),
            items.Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal));
    }

    public async Task<BudgetItemEditSnapshot?> GetItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty) throw new ArgumentException("Budget item ID is required.", nameof(itemId));

        var item = await dbContext.BudgetItems
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null) return null;

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.FiscalYearId, cancellationToken);
        var version = await dbContext.BudgetVersions
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.BudgetVersionId, cancellationToken);

        var budgetSections = await LoadOptionsAsync(dbContext.BudgetSections, cancellationToken);
        var financeTypes = await LoadOptionsAsync(dbContext.FinanceTypes, cancellationToken);
        var departments = await LoadOptionsAsync(dbContext.Departments, cancellationToken);
        var locations = await LoadOptionsAsync(dbContext.Locations, cancellationToken);
        var needLevels = await LoadOptionsAsync(dbContext.NeedLevels, cancellationToken);
        var categories = await LoadOptionsAsync(dbContext.InternalCategories, cancellationToken);
        var frequencies = await LoadOptionsAsync(dbContext.Frequencies, cancellationToken);

        return new(
            item.Id,
            item.FiscalYearId,
            fiscalYear.DisplayName,
            item.BudgetVersionId,
            version.Name,
            version.IsLocked,
            item.StableIdentifier,
            item.ItemNumber,
            item.Description,
            item.ReasonPurpose,
            item.PurchaseType,
            item.BudgetSectionId,
            item.FinanceTypeId,
            item.DepartmentId,
            item.LocationId,
            item.NeedLevelId,
            item.InternalCategoryId,
            item.FrequencyId,
            item.Quantity,
            item.UnitCost,
            item.PlannedTotal,
            item.ApprovedTotal,
            item.RevisedTotal,
            item.EstimatedPurchaseDate,
            item.RenewalDate,
            item.Status,
            budgetSections,
            financeTypes,
            departments,
            locations,
            needLevels,
            categories,
            frequencies);
    }

    public async Task<Guid> AddItemAsync(
        Guid fiscalYearId,
        Guid versionId,
        string itemNumber,
        string description,
        decimal quantity,
        decimal unitCost,
        CancellationToken cancellationToken = default)
    {
        var version = await GetEditableVersionAsync(fiscalYearId, versionId, cancellationToken);

        itemNumber = itemNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(itemNumber)) throw new ArgumentException("Item number is required.", nameof(itemNumber));

        if (await dbContext.BudgetItems.AnyAsync(
            x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == version.Id && x.ItemNumber == itemNumber,
            cancellationToken))
            throw new InvalidOperationException("That item number already exists in the selected budget version.");

        var stableIdentifier = $"LF-{Guid.NewGuid():N}";
        var item = new BudgetItem(
            fiscalYearId,
            version.Id,
            stableIdentifier,
            itemNumber,
            description,
            quantity,
            unitCost);

        dbContext.BudgetItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public async Task UpdateItemAsync(
        Guid itemId,
        string itemNumber,
        string description,
        string? reasonPurpose,
        PurchaseType purchaseType,
        decimal quantity,
        decimal unitCost,
        DateOnly? estimatedPurchaseDate,
        DateOnly? renewalDate,
        Guid? budgetSectionId,
        Guid? financeTypeId,
        Guid? departmentId,
        Guid? locationId,
        Guid? needLevelId,
        Guid? internalCategoryId,
        Guid? frequencyId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.BudgetItems
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");

        await GetEditableVersionAsync(item.FiscalYearId, item.BudgetVersionId, cancellationToken);

        var normalizedNumber = itemNumber?.Trim() ?? string.Empty;
        if (!string.Equals(item.ItemNumber, normalizedNumber, StringComparison.OrdinalIgnoreCase) &&
            await dbContext.BudgetItems.AnyAsync(
                x => x.Id != item.Id &&
                     x.FiscalYearId == item.FiscalYearId &&
                     x.BudgetVersionId == item.BudgetVersionId &&
                     x.ItemNumber == normalizedNumber,
                cancellationToken))
            throw new InvalidOperationException("That item number already exists in the selected budget version.");

        await ValidateLookupAsync(dbContext.BudgetSections, budgetSectionId, "Budget section", cancellationToken);
        await ValidateLookupAsync(dbContext.FinanceTypes, financeTypeId, "Finance type", cancellationToken);
        await ValidateLookupAsync(dbContext.Departments, departmentId, "Department", cancellationToken);
        await ValidateLookupAsync(dbContext.Locations, locationId, "Location", cancellationToken);
        await ValidateLookupAsync(dbContext.NeedLevels, needLevelId, "Need level", cancellationToken);
        await ValidateLookupAsync(dbContext.InternalCategories, internalCategoryId, "Internal category", cancellationToken);
        await ValidateLookupAsync(dbContext.Frequencies, frequencyId, "Frequency", cancellationToken);

        item.ChangeCost(quantity, unitCost);
        item.UpdatePlanningDetails(
            normalizedNumber,
            description,
            reasonPurpose,
            purchaseType,
            estimatedPurchaseDate,
            renewalDate,
            budgetSectionId,
            financeTypeId,
            departmentId,
            locationId,
            needLevelId,
            internalCategoryId,
            frequencyId);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<BudgetVersion> GetEditableVersionAsync(
        Guid fiscalYearId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.BudgetVersions
            .SingleOrDefaultAsync(x => x.Id == versionId && x.FiscalYearId == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget version was not found for the selected fiscal year.");

        if (version.IsLocked)
            throw new InvalidOperationException("Locked budget versions cannot be edited.");

        return version;
    }

    private static async Task<IReadOnlyList<LookupOption>> LoadOptionsAsync<TEntity>(
        DbSet<TEntity> set,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        return await set
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new LookupOption(x.Id, x.Code, x.Name))
            .ToListAsync(cancellationToken);
    }

    private static async Task ValidateLookupAsync<TEntity>(
        DbSet<TEntity> set,
        Guid? id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (id is null) return;
        if (id == Guid.Empty || !await set.AnyAsync(x => x.Id == id && x.IsActive, cancellationToken))
            throw new InvalidOperationException($"{label} is not an active lookup value.");
    }

    private static Guid? ResolveFiscalYear(IReadOnlyList<FiscalYearSummary> years, Guid? requested)
    {
        if (requested is not null && years.Any(x => x.Id == requested)) return requested;
        return years.FirstOrDefault(x => x.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
    }

    private static BudgetVersionSummary? ResolveVersion(IReadOnlyList<BudgetVersionSummary> versions, Guid? requested)
    {
        if (requested is not null)
        {
            var match = versions.SingleOrDefault(x => x.Id == requested);
            if (match is not null) return match;
        }

        return versions
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault();
    }
}
