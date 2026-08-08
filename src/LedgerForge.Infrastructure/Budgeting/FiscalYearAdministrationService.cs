using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record FiscalYearSummary(
    Guid Id,
    string DisplayName,
    DateOnly StartDate,
    DateOnly EndDate,
    int PlanningYear,
    FiscalYearStatus Status,
    bool IsCurrent,
    bool IsLocked,
    int PeriodCount,
    int VersionCount);

public sealed record FiscalPeriodSummary(
    Guid Id,
    int PeriodNumber,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsClosed,
    string? ClosedBy,
    DateTimeOffset? ClosedAtUtc);

public sealed record BudgetVersionSummary(
    Guid Id,
    Guid FiscalYearId,
    string Name,
    BudgetVersionType VersionType,
    int VersionNumber,
    DateOnly? EffectiveDate,
    bool IsLocked,
    DateTimeOffset? ApprovedAtUtc);

public sealed record FiscalCloseReadiness(
    bool CanClose,
    IReadOnlyList<string> Blockers,
    int OpenPeriods,
    int PendingBudgetItems,
    int OpenPurchaseOrders,
    int OpenInvoices,
    int OpenAmendments,
    decimal OutstandingCommitment);

public sealed class FiscalYearAdministrationService(
    LedgerForgeDbContext dbContext,
    OutstandingCommitmentService outstandingCommitmentService)
{
    public async Task<IReadOnlyList<FiscalYearSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.FiscalYears
            .AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .Select(x => new FiscalYearSummary(
                x.Id,
                x.DisplayName,
                x.StartDate,
                x.EndDate,
                x.PlanningYear,
                x.Status,
                x.IsCurrent,
                x.LockedAtUtc != null,
                dbContext.FiscalPeriods.Count(period => period.FiscalYearId == x.Id),
                dbContext.BudgetVersions.Count(version => version.FiscalYearId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalPeriodSummary>> ListPeriodsAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
        => await dbContext.FiscalPeriods.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.PeriodNumber)
            .Select(x => new FiscalPeriodSummary(
                x.Id, x.PeriodNumber, x.Code, x.Name, x.StartDate, x.EndDate,
                x.IsClosed, x.ClosedBy, x.ClosedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BudgetVersionSummary>> ListVersionsAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BudgetVersions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.VersionNumber)
            .Select(x => new BudgetVersionSummary(
                x.Id,
                x.FiscalYearId,
                x.Name,
                x.VersionType,
                x.VersionNumber,
                x.EffectiveDate,
                x.IsLocked,
                x.ApprovedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        bool isCurrent,
        bool createMonthlyPeriods,
        string initialVersionName,
        CancellationToken cancellationToken = default)
    {
        displayName = displayName?.Trim() ?? string.Empty;
        if (await dbContext.FiscalYears.AnyAsync(x => x.DisplayName == displayName, cancellationToken))
            throw new InvalidOperationException("A fiscal year with this display name already exists.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (isCurrent)
        {
            var existingCurrent = await dbContext.FiscalYears.Where(x => x.IsCurrent).ToListAsync(cancellationToken);
            foreach (var existing in existingCurrent) existing.SetCurrent(false);
        }

        var fiscalYear = new FiscalYear(displayName, startDate, endDate, planningYear);
        fiscalYear.UpdateDetails(displayName, startDate, endDate, planningYear, description);
        fiscalYear.SetCurrent(isCurrent);
        fiscalYear.SetStatus(FiscalYearStatus.Planning);
        dbContext.FiscalYears.Add(fiscalYear);

        if (createMonthlyPeriods)
        {
            foreach (var period in BuildMonthlyPeriods(fiscalYear.Id, startDate, endDate))
                dbContext.FiscalPeriods.Add(period);
        }

        var initialVersion = new BudgetVersion(
            fiscalYear.Id,
            string.IsNullOrWhiteSpace(initialVersionName) ? "Initial Planning" : initialVersionName,
            BudgetVersionType.InitialPlanning,
            versionNumber: 1,
            effectiveDate: startDate);
        dbContext.BudgetVersions.Add(initialVersion);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return fiscalYear.Id;
    }

    public async Task SetCurrentAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year ID is required.", nameof(fiscalYearId));

        var years = await dbContext.FiscalYears.ToListAsync(cancellationToken);
        var selected = years.SingleOrDefault(x => x.Id == fiscalYearId)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");

        foreach (var year in years) year.SetCurrent(year.Id == selected.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClosePeriodAsync(
        Guid fiscalYearId,
        Guid periodId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var year = await RequireYearAsync(fiscalYearId, tracking: true, cancellationToken);
        if (year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Closed or archived fiscal years cannot change period state.");
        var period = await dbContext.FiscalPeriods.SingleOrDefaultAsync(
            x => x.Id == periodId && x.FiscalYearId == fiscalYearId,
            cancellationToken) ?? throw new KeyNotFoundException("Fiscal period was not found in this fiscal year.");
        period.Close(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FiscalCloseReadiness> GetCloseReadinessAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireYearAsync(fiscalYearId, tracking: false, cancellationToken);

        var openPeriods = await dbContext.FiscalPeriods.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == fiscalYearId && !x.IsClosed, cancellationToken);
        var pendingBudgetItems = await dbContext.BudgetItems.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == fiscalYearId && x.Status == BudgetItemStatus.Submitted, cancellationToken);
        var openPurchaseOrders = await dbContext.PurchaseOrders.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == fiscalYearId &&
                x.State != PurchaseOrderState.Closed &&
                x.State != PurchaseOrderState.Cancelled &&
                x.State != PurchaseOrderState.Rejected &&
                x.State != PurchaseOrderState.Issued,
                cancellationToken);
        var openInvoices = await dbContext.Invoices.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == fiscalYearId &&
                x.State != InvoiceState.Posted &&
                x.State != InvoiceState.Cancelled &&
                x.State != InvoiceState.Rejected,
                cancellationToken);
        var openAmendments = await dbContext.BudgetAmendments.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == fiscalYearId &&
                x.State != BudgetAmendmentState.Approved &&
                x.State != BudgetAmendmentState.Rejected &&
                x.State != BudgetAmendmentState.Cancelled,
                cancellationToken);
        var outstandingCommitment = await outstandingCommitmentService.GetTotalForFiscalYearAsync(fiscalYearId, cancellationToken);

        var blockers = new List<string>();
        if (openPeriods > 0) blockers.Add($"{openPeriods} fiscal period(s) remain open.");
        if (pendingBudgetItems > 0) blockers.Add($"{pendingBudgetItems} budget item(s) are awaiting approval.");
        if (openPurchaseOrders > 0) blockers.Add($"{openPurchaseOrders} purchase order(s) are still draft, pending, or approved but not issued/closed.");
        if (openInvoices > 0) blockers.Add($"{openInvoices} invoice(s) are not posted/cancelled/rejected.");
        if (openAmendments > 0) blockers.Add($"{openAmendments} budget amendment(s) are unresolved.");
        if (outstandingCommitment > 0m) blockers.Add($"Outstanding issued purchase-order commitments total {outstandingCommitment:C2}.");

        return new(
            blockers.Count == 0,
            blockers,
            openPeriods,
            pendingBudgetItems,
            openPurchaseOrders,
            openInvoices,
            openAmendments,
            outstandingCommitment);
    }

    public async Task CloseAsync(
        Guid fiscalYearId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        var readiness = await GetCloseReadinessAsync(fiscalYearId, cancellationToken);
        if (!readiness.CanClose)
            throw new InvalidOperationException("Fiscal year cannot be closed: " + string.Join(" ", readiness.Blockers));

        var year = await RequireYearAsync(fiscalYearId, tracking: true, cancellationToken);
        if (year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Fiscal year is already closed or archived.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var versions = await dbContext.BudgetVersions.Where(x => x.FiscalYearId == fiscalYearId).ToListAsync(cancellationToken);
        foreach (var version in versions) version.Lock();
        year.Close(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> RolloverAsync(
        Guid sourceFiscalYearId,
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        string initialVersionName,
        bool createMonthlyPeriods = true,
        CancellationToken cancellationToken = default)
    {
        var sourceYear = await RequireYearAsync(sourceFiscalYearId, tracking: false, cancellationToken);
        if (sourceYear.Status != FiscalYearStatus.Closed)
            throw new InvalidOperationException("Only a closed fiscal year can be rolled forward.");
        displayName = displayName?.Trim() ?? string.Empty;
        if (await dbContext.FiscalYears.AnyAsync(x => x.DisplayName == displayName, cancellationToken))
            throw new InvalidOperationException("A fiscal year with this display name already exists.");

        var sourceVersion = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == sourceFiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The source fiscal year does not have a budget version to roll forward.");
        var sourceItems = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == sourceFiscalYearId && x.BudgetVersionId == sourceVersion.Id &&
                x.Status != BudgetItemStatus.Cancelled &&
                x.Status != BudgetItemStatus.Archived &&
                x.Status != BudgetItemStatus.Denied)
            .OrderBy(x => x.ItemNumber)
            .ToListAsync(cancellationToken);
        var sourceItemIds = sourceItems.Select(x => x.Id).ToArray();
        var sourceAllocations = sourceItemIds.Length == 0
            ? []
            : await dbContext.BudgetItemAllocations.AsNoTracking()
                .Where(x => sourceItemIds.Contains(x.BudgetItemId))
                .ToListAsync(cancellationToken);
        var sourcePeriods = await dbContext.FiscalPeriods.AsNoTracking()
            .Where(x => x.FiscalYearId == sourceFiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.PeriodNumber, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var currentYears = await dbContext.FiscalYears.Where(x => x.IsCurrent).ToListAsync(cancellationToken);
        foreach (var current in currentYears) current.SetCurrent(false);

        var targetYear = new FiscalYear(displayName, startDate, endDate, planningYear);
        targetYear.UpdateDetails(displayName, startDate, endDate, planningYear, description);
        targetYear.SetStatus(FiscalYearStatus.Planning);
        targetYear.SetCurrent(true);
        dbContext.FiscalYears.Add(targetYear);

        var targetPeriods = createMonthlyPeriods
            ? BuildMonthlyPeriods(targetYear.Id, startDate, endDate).ToList()
            : [];
        dbContext.FiscalPeriods.AddRange(targetPeriods);
        var targetPeriodIds = targetPeriods.ToDictionary(x => x.PeriodNumber, x => x.Id);

        var targetVersion = new BudgetVersion(
            targetYear.Id,
            string.IsNullOrWhiteSpace(initialVersionName) ? "Rolled Forward Planning" : initialVersionName,
            BudgetVersionType.InitialPlanning,
            1,
            basedOnVersionId: sourceVersion.Id,
            effectiveDate: startDate,
            notes: $"Planning baseline rolled forward from {sourceYear.DisplayName}.");
        dbContext.BudgetVersions.Add(targetVersion);

        var targetItemIds = new Dictionary<Guid, Guid>();
        foreach (var source in sourceItems)
        {
            var baseline = source.RevisedTotal ?? source.ApprovedTotal ?? source.PlannedTotal;
            var target = new BudgetItem(
                targetYear.Id,
                targetVersion.Id,
                source.StableIdentifier,
                source.ItemNumber,
                source.Description,
                1m,
                baseline);
            target.UpdatePlanningDetails(
                source.ItemNumber,
                source.Description,
                source.ReasonPurpose,
                source.PurchaseType,
                ShiftDate(source.EstimatedPurchaseDate, sourceYear.StartDate, targetYear.StartDate),
                ShiftDate(source.RenewalDate, sourceYear.StartDate, targetYear.StartDate),
                source.BudgetSectionId,
                source.FinanceTypeId,
                source.DepartmentId,
                source.LocationId,
                source.NeedLevelId,
                source.InternalCategoryId,
                source.FrequencyId);
            dbContext.BudgetItems.Add(target);
            targetItemIds[source.Id] = target.Id;
        }

        foreach (var allocation in sourceAllocations)
        {
            if (!targetItemIds.TryGetValue(allocation.BudgetItemId, out var targetItemId)) continue;
            Guid? targetPeriodId = null;
            if (allocation.FiscalPeriodId is Guid sourcePeriodId &&
                sourcePeriods.TryGetValue(sourcePeriodId, out var periodNumber) &&
                targetPeriodIds.TryGetValue(periodNumber, out var mappedPeriodId))
                targetPeriodId = mappedPeriodId;

            dbContext.BudgetItemAllocations.Add(new BudgetItemAllocation(
                targetItemId,
                allocation.Method,
                allocation.Percentage,
                allocation.Amount,
                allocation.DepartmentId,
                allocation.LocationId,
                allocation.FinanceAccountId,
                targetPeriodId,
                allocation.Notes));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return targetYear.Id;
    }

    private async Task<FiscalYear> RequireYearAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Fiscal year ID is required.", nameof(id));
        var query = tracking ? dbContext.FiscalYears : dbContext.FiscalYears.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
    }

    private static DateOnly? ShiftDate(DateOnly? date, DateOnly sourceStart, DateOnly targetStart)
        => date is null ? null : targetStart.AddDays(date.Value.DayNumber - sourceStart.DayNumber);

    private static IReadOnlyList<FiscalPeriod> BuildMonthlyPeriods(
        Guid fiscalYearId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var periods = new List<FiscalPeriod>();
        var cursor = startDate;
        var periodNumber = 1;

        while (cursor <= endDate)
        {
            var monthEnd = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var periodEnd = monthEnd < endDate ? monthEnd : endDate;
            periods.Add(new FiscalPeriod(
                fiscalYearId,
                periodNumber,
                $"P{periodNumber:00}",
                cursor.ToString("MMM yyyy"),
                cursor,
                periodEnd));

            if (periodEnd == endDate) break;
            cursor = periodEnd.AddDays(1);
            periodNumber++;
        }

        return periods;
    }
}
