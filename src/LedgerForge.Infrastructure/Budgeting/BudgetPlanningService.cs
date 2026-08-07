using LedgerForge.Domain.Budgeting;
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

    public async Task<Guid> AddItemAsync(
        Guid fiscalYearId,
        Guid versionId,
        string itemNumber,
        string description,
        decimal quantity,
        decimal unitCost,
        CancellationToken cancellationToken = default)
    {
        var version = await dbContext.BudgetVersions
            .SingleOrDefaultAsync(x => x.Id == versionId && x.FiscalYearId == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget version was not found for the selected fiscal year.");

        if (version.IsLocked)
            throw new InvalidOperationException("Locked budget versions cannot accept new planning items.");

        itemNumber = itemNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(itemNumber)) throw new ArgumentException("Item number is required.", nameof(itemNumber));

        if (await dbContext.BudgetItems.AnyAsync(
            x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == versionId && x.ItemNumber == itemNumber,
            cancellationToken))
            throw new InvalidOperationException("That item number already exists in the selected budget version.");

        var stableIdentifier = $"LF-{fiscalYearId:N}-{Guid.NewGuid():N}";
        var item = new BudgetItem(
            fiscalYearId,
            versionId,
            stableIdentifier,
            itemNumber,
            description,
            quantity,
            unitCost);

        dbContext.BudgetItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item.Id;
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
