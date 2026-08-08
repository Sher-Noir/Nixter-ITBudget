using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Importing;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Dashboard;

public sealed record DashboardRecentBudgetItem(
    Guid Id,
    string ItemNumber,
    string Description,
    BudgetItemStatus Status,
    decimal PlannedTotal,
    DateTimeOffset CreatedAtUtc);

public sealed record DashboardRenewalItem(
    Guid Id,
    string Source,
    Guid? BudgetItemId,
    Guid? ContractId,
    string Reference,
    string Description,
    DateOnly RenewalDate,
    decimal EstimatedAmount);

public sealed record DashboardMonthlySpend(string Label, decimal Planned, decimal Actual);

public sealed record DashboardCategorySpend(
    string Category,
    decimal Budget,
    decimal Actual,
    decimal Variance);

public sealed record DashboardOpenPurchaseOrder(
    Guid Id,
    string Number,
    string Vendor,
    string Category,
    string Location,
    PurchaseOrderState State,
    decimal Amount);

public sealed record DashboardActivityItem(
    Guid Id,
    string EntityType,
    string Action,
    string Actor,
    DateTimeOffset OccurredAtUtc);

public sealed record DashboardSnapshot(
    Guid? FiscalYearId,
    string FiscalYearName,
    Guid? BudgetVersionId,
    string BudgetVersionName,
    decimal PlannedBudget,
    decimal ApprovedBudget,
    decimal RevisedBudget,
    decimal Committed,
    decimal Actual,
    decimal Available,
    decimal Forecast,
    bool ForecastIsPublished,
    int PendingApprovals,
    int RenewalsDueIn30Days,
    int BudgetItemCount,
    int ImportBatchesNeedingReview,
    IReadOnlyList<DashboardRenewalItem> UpcomingRenewals,
    IReadOnlyList<DashboardRecentBudgetItem> RecentBudgetItems,
    IReadOnlyList<DashboardMonthlySpend> MonthlySpend,
    IReadOnlyList<DashboardCategorySpend> CategorySpend,
    IReadOnlyList<DashboardOpenPurchaseOrder> OpenPurchaseOrders,
    IReadOnlyList<DashboardActivityItem> RecentActivity);

public sealed class DashboardService(
    LedgerForgeDbContext dbContext,
    OutstandingCommitmentService outstandingCommitmentService,
    ForecastService forecastService,
    ApprovalQueueService approvalQueueService,
    RenewalProjectionService renewalProjectionService)
{
    private sealed record BudgetRow(
        Guid Id,
        string ItemNumber,
        string Description,
        BudgetItemStatus Status,
        decimal PlannedTotal,
        decimal? ApprovedTotal,
        decimal? RevisedTotal,
        DateOnly? EstimatedPurchaseDate,
        Guid? InternalCategoryId,
        Guid? LocationId,
        DateTimeOffset CreatedAtUtc);

    private sealed record ActualRow(DateOnly TransactionDate, decimal Amount, Guid? BudgetItemId);

    public async Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (fiscalYear is null)
        {
            var importReviewCount = await CountImportReviewAsync(cancellationToken);
            return Empty(importReviewCount);
        }

        var actualRows = await dbContext.ActualTransactions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id)
            .Select(x => new ActualRow(x.TransactionDate, x.Amount, x.BudgetItemId))
            .ToListAsync(cancellationToken);
        var actual = actualRows.Sum(x => x.Amount);
        var committed = await outstandingCommitmentService.GetTotalForFiscalYearAsync(fiscalYear.Id, cancellationToken);
        var pendingApprovals = (await approvalQueueService.GetAsync(fiscalYear.Id, cancellationToken)).TotalCount;

        var version = await dbContext.BudgetVersions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            var importReviewCount = await CountImportReviewAsync(cancellationToken);
            return Empty(importReviewCount) with
            {
                FiscalYearId = fiscalYear.Id,
                FiscalYearName = fiscalYear.DisplayName,
                Committed = committed,
                Actual = actual,
                Available = -committed - actual,
                Forecast = committed + actual,
                PendingApprovals = pendingApprovals,
                RecentActivity = await LoadRecentActivityAsync(cancellationToken)
            };
        }

        var itemRows = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id && x.BudgetVersionId == version.Id)
            .Select(x => new BudgetRow(
                x.Id,
                x.ItemNumber,
                x.Description,
                x.Status,
                x.PlannedTotal,
                x.ApprovedTotal,
                x.RevisedTotal,
                x.EstimatedPurchaseDate,
                x.InternalCategoryId,
                x.LocationId,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var planned = itemRows.Sum(x => x.PlannedTotal);
        var approved = itemRows.Sum(x => x.ApprovedTotal ?? 0m);
        var revised = itemRows.Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal);
        var available = revised - committed - actual;
        var publishedForecast = await forecastService.GetLatestPublishedTotalAsync(fiscalYear.Id, version.Id, cancellationToken);
        var forecast = publishedForecast ?? Math.Max(revised, committed + actual);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var renewalCutoff = today.AddDays(30);
        var renewalMatches = (await renewalProjectionService.GetForFiscalYearAsync(fiscalYear.Id, cancellationToken))
            .Where(x => x.RenewalDate >= today && x.RenewalDate <= renewalCutoff)
            .OrderBy(x => x.RenewalDate)
            .ToArray();

        var upcomingRenewals = renewalMatches
            .Take(5)
            .Select(x => new DashboardRenewalItem(
                x.Id,
                x.Source,
                x.BudgetItemId,
                x.ContractId,
                x.Reference,
                x.Description,
                x.RenewalDate,
                x.EstimatedAmount))
            .ToArray();

        var recentItems = itemRows
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.ItemNumber)
            .Take(5)
            .Select(x => new DashboardRecentBudgetItem(
                x.Id,
                x.ItemNumber,
                x.Description,
                x.Status,
                x.PlannedTotal,
                x.CreatedAtUtc))
            .ToArray();

        var categoryNames = await dbContext.InternalCategories.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var itemCategory = itemRows.ToDictionary(
            x => x.Id,
            x => x.InternalCategoryId is not null && categoryNames.TryGetValue(x.InternalCategoryId.Value, out var name) ? name : "Unassigned");

        var monthlySpend = BuildMonthlySpend(fiscalYear.StartDate, fiscalYear.EndDate, itemRows, actualRows);
        var categorySpend = BuildCategorySpend(itemRows, actualRows, itemCategory);
        var openPurchaseOrders = await LoadOpenPurchaseOrdersAsync(
            fiscalYear.Id,
            itemRows.ToDictionary(x => x.Id, x => (x.InternalCategoryId, x.LocationId)),
            categoryNames,
            locationNames,
            cancellationToken);

        return new(
            fiscalYear.Id,
            fiscalYear.DisplayName,
            version.Id,
            version.Name,
            planned,
            approved,
            revised,
            committed,
            actual,
            available,
            forecast,
            publishedForecast is not null,
            pendingApprovals,
            renewalMatches.Length,
            itemRows.Count,
            await CountImportReviewAsync(cancellationToken),
            upcomingRenewals,
            recentItems,
            monthlySpend,
            categorySpend,
            openPurchaseOrders,
            await LoadRecentActivityAsync(cancellationToken));
    }

    private static IReadOnlyList<DashboardMonthlySpend> BuildMonthlySpend(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<BudgetRow> items,
        IReadOnlyList<ActualRow> actuals)
    {
        var result = new List<DashboardMonthlySpend>();
        var cursor = new DateOnly(startDate.Year, startDate.Month, 1);
        var last = new DateOnly(endDate.Year, endDate.Month, 1);
        while (cursor <= last)
        {
            var planned = items
                .Where(x => x.EstimatedPurchaseDate is not null &&
                            x.EstimatedPurchaseDate.Value.Year == cursor.Year &&
                            x.EstimatedPurchaseDate.Value.Month == cursor.Month)
                .Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal);
            var actual = actuals
                .Where(x => x.TransactionDate.Year == cursor.Year && x.TransactionDate.Month == cursor.Month)
                .Sum(x => x.Amount);
            result.Add(new DashboardMonthlySpend(cursor.ToString("MMM"), planned, actual));
            cursor = cursor.AddMonths(1);
        }
        return result;
    }

    private static IReadOnlyList<DashboardCategorySpend> BuildCategorySpend(
        IReadOnlyList<BudgetRow> items,
        IReadOnlyList<ActualRow> actuals,
        IReadOnlyDictionary<Guid, string> itemCategory)
    {
        var budget = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var spend = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var category = itemCategory.GetValueOrDefault(item.Id, "Unassigned");
            budget[category] = budget.GetValueOrDefault(category) + (item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal);
        }
        foreach (var row in actuals)
        {
            var category = row.BudgetItemId is not null
                ? itemCategory.GetValueOrDefault(row.BudgetItemId.Value, "Unassigned")
                : "Unassigned";
            spend[category] = spend.GetValueOrDefault(category) + row.Amount;
        }

        return budget.Keys.Union(spend.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(category => new DashboardCategorySpend(
                category,
                budget.GetValueOrDefault(category),
                spend.GetValueOrDefault(category),
                budget.GetValueOrDefault(category) - spend.GetValueOrDefault(category)))
            .OrderByDescending(x => x.Actual)
            .ThenByDescending(x => x.Budget)
            .ThenBy(x => x.Category)
            .Take(8)
            .ToArray();
    }

    private async Task<IReadOnlyList<DashboardOpenPurchaseOrder>> LoadOpenPurchaseOrdersAsync(
        Guid fiscalYearId,
        IReadOnlyDictionary<Guid, (Guid? CategoryId, Guid? LocationId)> itemDimensions,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyDictionary<Guid, string> locationNames,
        CancellationToken cancellationToken)
    {
        var orders = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId &&
                        x.State != PurchaseOrderState.Closed &&
                        x.State != PurchaseOrderState.Cancelled &&
                        x.State != PurchaseOrderState.Rejected)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .ToListAsync(cancellationToken);
        if (orders.Count == 0) return [];

        var orderIds = orders.Select(x => x.Id).ToArray();
        var lines = await dbContext.PurchaseOrderLines.AsNoTracking()
            .Where(x => orderIds.Contains(x.PurchaseOrderId))
            .Select(x => new { x.PurchaseOrderId, x.LineTotal, x.BudgetItemId, x.LocationId })
            .ToListAsync(cancellationToken);
        var vendorIds = orders.Select(x => x.VendorId).Distinct().ToArray();
        var vendors = await dbContext.Vendors.AsNoTracking()
            .Where(x => vendorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return orders.Select(order =>
        {
            var orderLines = lines.Where(x => x.PurchaseOrderId == order.Id).ToArray();
            var amount = orderLines.Sum(x => x.LineTotal);
            var categories = orderLines
                .Where(x => x.BudgetItemId is not null && itemDimensions.ContainsKey(x.BudgetItemId.Value))
                .Select(x => itemDimensions[x.BudgetItemId!.Value].CategoryId)
                .Where(x => x is not null && categoryNames.ContainsKey(x.Value))
                .Select(x => categoryNames[x!.Value])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var locations = orderLines
                .Select(x => x.LocationId ?? (x.BudgetItemId is not null && itemDimensions.TryGetValue(x.BudgetItemId.Value, out var dimensions) ? dimensions.LocationId : null))
                .Where(x => x is not null && locationNames.ContainsKey(x.Value))
                .Select(x => locationNames[x!.Value])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new DashboardOpenPurchaseOrder(
                order.Id,
                order.Number,
                vendors.GetValueOrDefault(order.VendorId, "Unknown vendor"),
                categories.Length switch { 0 => "—", 1 => categories[0], _ => "Multiple" },
                locations.Length switch { 0 => "—", 1 => locations[0], _ => "Multiple" },
                order.State,
                amount);
        }).ToArray();
    }

    private async Task<IReadOnlyList<DashboardActivityItem>> LoadRecentActivityAsync(CancellationToken cancellationToken)
        => await dbContext.AuditEvents.AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(6)
            .Select(x => new DashboardActivityItem(
                x.Id,
                x.EntityType,
                x.Action.ToString(),
                x.Actor,
                x.OccurredAtUtc))
            .ToListAsync(cancellationToken);

    private async Task<int> CountImportReviewAsync(CancellationToken cancellationToken)
        => await dbContext.ImportBatches.CountAsync(
            x => x.Status == ImportBatchStatus.PreviewReady && x.AcceptedBy == null,
            cancellationToken);

    private static DashboardSnapshot Empty(int importReviewCount)
        => new(
            null,
            "No fiscal year",
            null,
            "No budget version",
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            0m,
            false,
            0,
            0,
            0,
            importReviewCount,
            [],
            [],
            [],
            [],
            [],
            []);
}
