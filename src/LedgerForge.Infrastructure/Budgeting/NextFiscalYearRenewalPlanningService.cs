using System.Data;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record RenewalPlanningYear(
    Guid Id, string Name, DateOnly StartDate, DateOnly EndDate,
    bool IsCurrent, bool IsLocked, FiscalYearStatus Status);

public sealed record RenewalPlanningVersion(Guid Id, string Name, int VersionNumber, bool IsLocked);

public sealed record RenewalPlanningCandidate(
    string Key, string Source, string Reference, string Description,
    string? Vendor, DateOnly RenewalDate, DateOnly? NoticeDate,
    decimal Quantity, decimal SourceEstimate, decimal ProjectedUnitCost,
    decimal ProjectedTotal, bool AlreadyPlanned, bool DateNeedsReview,
    string StableIdentifier, string ItemNumber, string? ReasonPurpose,
    string? Notes, Guid? VendorId, Guid? BudgetSectionId, Guid? FinanceTypeId,
    Guid? DepartmentId, Guid? LocationId, Guid? NeedLevelId,
    Guid? InternalCategoryId, Guid? FrequencyId);

public sealed record RenewalPlanningSnapshot(
    IReadOnlyList<RenewalPlanningYear> Years, Guid? SourceFiscalYearId,
    Guid? TargetFiscalYearId, IReadOnlyList<RenewalPlanningVersion> Versions,
    Guid? TargetVersionId, decimal UpliftPercent,
    IReadOnlyList<RenewalPlanningCandidate> Candidates)
{
    public int NewCandidateCount => Candidates.Count(x => !x.AlreadyPlanned);
    public decimal ProjectedTotal => Candidates.Where(x => !x.AlreadyPlanned).Sum(x => x.ProjectedTotal);
    public decimal BaselineTotal => Candidates.Where(x => !x.AlreadyPlanned).Sum(x => x.SourceEstimate);
}

public sealed class NextFiscalYearRenewalPlanningService(
    LedgerForgeDbContext dbContext,
    RenewalProjectionService renewalProjectionService)
{
    public async Task<RenewalPlanningSnapshot> GetAsync(
        Guid? sourceFiscalYearId, Guid? targetFiscalYearId, Guid? targetVersionId,
        decimal upliftPercent, CancellationToken cancellationToken = default)
    {
        ValidateUplift(upliftPercent);
        var fiscalYears = await dbContext.FiscalYears.AsNoTracking()
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);
        var years = fiscalYears.Select(x => new RenewalPlanningYear(
            x.Id, x.DisplayName, x.StartDate, x.EndDate,
            x.IsCurrent, x.IsLocked, x.Status)).ToArray();
        var source = fiscalYears.FirstOrDefault(x => x.Id == sourceFiscalYearId)
            ?? fiscalYears.FirstOrDefault(x => x.IsCurrent)
            ?? fiscalYears.LastOrDefault();
        if (source is null)
            return new(years, null, null, [], null, upliftPercent, []);

        var target = fiscalYears.FirstOrDefault(x => x.Id == targetFiscalYearId && x.StartDate > source.StartDate)
            ?? fiscalYears.FirstOrDefault(x => x.StartDate > source.StartDate &&
                x.Status is not (FiscalYearStatus.Closed or FiscalYearStatus.Archived));
        if (target is null)
            return new(years, source.Id, null, [], null, upliftPercent, []);

        var versions = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == target.Id)
            .OrderBy(x => x.VersionNumber)
            .Select(x => new RenewalPlanningVersion(x.Id, x.Name, x.VersionNumber, x.IsLocked))
            .ToListAsync(cancellationToken);
        var selectedVersion = versions.FirstOrDefault(x => x.Id == targetVersionId)
            ?? versions.FirstOrDefault(x => !x.IsLocked);
        if (selectedVersion is null)
            return new(years, source.Id, target.Id, versions, null, upliftPercent, []);

        var sourceVersionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == source.Id)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (sourceVersionId is null)
            return new(years, source.Id, target.Id, versions, selectedVersion.Id, upliftPercent, []);

        var sourceItems = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == source.Id && x.BudgetVersionId == sourceVersionId.Value &&
                !x.IsArchived &&
                x.Status != BudgetItemStatus.Archived &&
                x.Status != BudgetItemStatus.Cancelled &&
                x.Status != BudgetItemStatus.Denied)
            .ToListAsync(cancellationToken);
        var targetItems = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.BudgetVersionId == selectedVersion.Id)
            .Select(x => new { x.StableIdentifier, x.ItemNumber })
            .ToListAsync(cancellationToken);
        var alreadyPlannedIds = targetItems.Select(x => x.StableIdentifier)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vendorIds = sourceItems.Select(x => x.VendorId).OfType<Guid>().Distinct().ToArray();
        var vendors = vendorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Vendors.AsNoTracking()
                .Where(x => vendorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var frequencies = await dbContext.Frequencies.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        // Contracts scheduled in the target year provide authoritative dates,
        // notice deadlines and estimates for linked source budget items.
        var projection = (await renewalProjectionService
            .GetForFiscalYearAsync(target.Id, cancellationToken))
            .Where(x => x.ContractId is not null &&
                !string.Equals(x.Status, ContractRenewalStatus.Terminate.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.RenewalDate)
            .ToArray();
        var contractIds = projection.Select(x => x.ContractId!.Value).Distinct().ToArray();
        var contractVendorIds = contractIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await dbContext.Contracts.AsNoTracking()
                .Where(x => contractIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.VendorId, cancellationToken);
        var linked = projection.Where(x => x.BudgetItemId is not null)
            .GroupBy(x => x.BudgetItemId!.Value)
            .ToDictionary(x => x.Key, x => x.First());
        var sourceItemIds = sourceItems.Select(x => x.Id).ToHashSet();
        var results = new List<RenewalPlanningCandidate>();

        foreach (var item in sourceItems.OrderBy(x => x.ItemNumber))
        {
            var recurring = item.FrequencyId is Guid frequencyId &&
                frequencies.TryGetValue(frequencyId, out var code) &&
                code is "MONTHLY" or "QUARTERLY" or "ANNUALLY";
            if (item.RenewalDate is null && item.PurchaseType != PurchaseType.Renewal &&
                !recurring && !linked.ContainsKey(item.Id))
                continue;

            linked.TryGetValue(item.Id, out var contract);
            Guid? renewalVendorId = item.VendorId;
            if (renewalVendorId is null && contract?.ContractId is Guid linkedContractId &&
                contractVendorIds.TryGetValue(linkedContractId, out var linkedVendorId))
                renewalVendorId = linkedVendorId;
            var fallbackDate = item.RenewalDate ?? item.EstimatedPurchaseDate;
            var (date, needsReview) = contract is not null
                ? (contract.RenewalDate, false)
                : ProjectRenewalDate(fallbackDate, source.StartDate, source.EndDate,
                    target.StartDate, target.EndDate);
            var baseline = contract is not null && contract.EstimatedAmount > 0m
                ? contract.EstimatedAmount
                : item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
            var qty = item.Quantity > 0m ? item.Quantity : 1m;
            var unitCost = EscalatedUnitCost(baseline, qty, upliftPercent);
            var stableIdentifier = item.StableIdentifier;
            results.Add(new RenewalPlanningCandidate(
                "B:" + item.Id.ToString("N"),
                contract is null ? "Recurring budget item" : "Linked contract renewal",
                item.ItemNumber, item.Description,
                contract?.Vendor ?? (item.VendorId is Guid vendorId ? vendors.GetValueOrDefault(vendorId) : null),
                date, contract?.NoticeDate, qty, baseline, unitCost, checked(qty * unitCost),
                alreadyPlannedIds.Contains(stableIdentifier), needsReview,
                stableIdentifier, item.ItemNumber, item.ReasonPurpose, item.Notes,
                renewalVendorId, item.BudgetSectionId, item.FinanceTypeId,
                item.DepartmentId, item.LocationId, item.NeedLevelId,
                item.InternalCategoryId, item.FrequencyId));
        }

        foreach (var contract in projection
            .Where(x => x.ContractId is not null &&
                (x.BudgetItemId is null || !sourceItemIds.Contains(x.BudgetItemId.Value)))
            .GroupBy(x => x.ContractId!.Value)
            .Select(x => x.First()))
        {
            var stableIdentifier = "CONTRACT-RENEWAL-" + contract.ContractId!.Value.ToString("N");
            var unitCost = EscalatedUnitCost(contract.EstimatedAmount, 1m, upliftPercent);
            // Contract-only entries retain a stable contract identifier to make
            // re-running the planner idempotent even when item numbers change.
            results.Add(new RenewalPlanningCandidate(
                "C:" + contract.ContractId.Value.ToString("N"),
                "Contract renewal", contract.Reference, contract.Description, contract.Vendor,
                contract.RenewalDate, contract.NoticeDate, 1m,
                contract.EstimatedAmount, unitCost, unitCost,
                alreadyPlannedIds.Contains(stableIdentifier), false,
                stableIdentifier, "", "Review renewal terms and notice deadline before commitment.",
                "Source contract: " + contract.Reference,
                contractVendorIds.TryGetValue(contract.ContractId.Value, out var contractVendorId)
                    ? contractVendorId : null,
                null, null, null, null, null, null, null));
        }

        return new(years, source.Id, target.Id, versions, selectedVersion.Id,
            upliftPercent, results.OrderBy(x => x.RenewalDate)
                .ThenBy(x => x.Description).ToArray());
    }

    /// <summary>
    /// Creates only Draft budget items, never purchase orders, actuals or signed
    /// contract renewals. Preflight, number allocation and SaveChanges share a
    /// serializable transaction; no partial carry-forward is permitted.
    /// </summary>
    public async Task<int> CreateDraftsAsync(
        Guid sourceFiscalYearId, Guid targetFiscalYearId, Guid targetVersionId,
        decimal upliftPercent, IReadOnlyCollection<string> selectedKeys,
        CancellationToken cancellationToken = default)
    {
        ValidateUplift(upliftPercent);
        if (selectedKeys is null || selectedKeys.Count is < 1 or > 200 ||
            selectedKeys.Any(string.IsNullOrWhiteSpace) ||
            selectedKeys.Distinct(StringComparer.Ordinal).Count() != selectedKeys.Count)
            throw new ArgumentException("Select between 1 and 200 distinct renewal candidates.", nameof(selectedKeys));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var preview = await GetAsync(sourceFiscalYearId, targetFiscalYearId, targetVersionId,
            upliftPercent, cancellationToken);
        if (preview.SourceFiscalYearId != sourceFiscalYearId ||
            preview.TargetFiscalYearId != targetFiscalYearId ||
            preview.TargetVersionId != targetVersionId)
            throw new InvalidOperationException("The source and target selections are no longer valid.");
        var year = await dbContext.FiscalYears.AsNoTracking()
            .SingleAsync(x => x.Id == targetFiscalYearId, cancellationToken);
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .SingleAsync(x => x.Id == targetVersionId, cancellationToken);
        if (year.LockedAtUtc is not null ||
            year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived ||
            version.IsLocked)
            throw new InvalidOperationException("A closed or locked target cannot receive renewal drafts.");

        var byKey = preview.Candidates.ToDictionary(x => x.Key, StringComparer.Ordinal);
        var chosen = selectedKeys.Select(key =>
        {
            if (!byKey.TryGetValue(key, out var candidate) || candidate.AlreadyPlanned)
                throw new InvalidOperationException("A selected renewal is missing or already planned. Refresh the preview.");
            return candidate;
        }).ToArray();

        var existingNumbers = (await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.BudgetVersionId == targetVersionId)
                .Select(x => x.ItemNumber).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNumber = existingNumbers
            .Select(x => int.TryParse(x, out var parsed) && parsed > 0 ? parsed : 0)
            .DefaultIfEmpty().Max() + 1;

        foreach (var candidate in chosen)
        {
            var number = candidate.ItemNumber;
            if (string.IsNullOrWhiteSpace(number) || !existingNumbers.Add(number))
            {
                while (!existingNumbers.Add(nextNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    nextNumber++;
                number = nextNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                nextNumber++;
            }

            var targetItem = new BudgetItem(
                targetFiscalYearId, targetVersionId, candidate.StableIdentifier,
                number, candidate.Description, candidate.Quantity, candidate.ProjectedUnitCost);
            targetItem.UpdatePlanningDetails(
                number, candidate.Description, candidate.ReasonPurpose,
                PurchaseType.Renewal, candidate.RenewalDate, candidate.RenewalDate,
                candidate.BudgetSectionId, candidate.FinanceTypeId,
                candidate.DepartmentId, candidate.LocationId, candidate.NeedLevelId,
                candidate.InternalCategoryId, candidate.FrequencyId);
            var sourceNote = "Renewal planning draft carried forward from " +
                preview.Years.Single(x => x.Id == sourceFiscalYearId).Name +
                ". Confirm date, quantity, vendor and final price before approval.";
            var notes = string.IsNullOrWhiteSpace(candidate.Notes)
                ? sourceNote
                : candidate.Notes.Length + sourceNote.Length + 1 <= 2000
                    ? candidate.Notes + "\n" + sourceNote
                    : candidate.Notes[..(2000 - sourceNote.Length - 1)] + "\n" + sourceNote;
            targetItem.UpdateVendorAndNotes(candidate.VendorId, notes);
            dbContext.BudgetItems.Add(targetItem);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return chosen.Length;
    }

    private static decimal EscalatedUnitCost(decimal baseline, decimal quantity, decimal upliftPercent)
        => decimal.Round(checked(baseline / quantity * (1m + upliftPercent / 100m)), 4,
            MidpointRounding.AwayFromZero);

    private static void ValidateUplift(decimal value)
    {
        if (value is < -100m or > 300m)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Estimated cost change must be between -100% and +300%.");
    }

    internal static (DateOnly Date, bool NeedsReview) ProjectRenewalDate(
        DateOnly? sourceDate, DateOnly sourceStart, DateOnly sourceEnd,
        DateOnly targetStart, DateOnly targetEnd)
    {
        if (sourceDate is null) return (targetStart, true);
        if (sourceDate >= targetStart && sourceDate <= targetEnd)
            return (sourceDate.Value, false);
        if (sourceDate >= sourceStart && sourceDate <= sourceEnd)
        {
            var projected = sourceDate.Value.AddYears(targetStart.Year - sourceStart.Year);
            if (projected >= targetStart && projected <= targetEnd)
                return (projected, false);
        }
        return (targetStart, true);
    }
}
