using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Importing;
using LedgerForge.Infrastructure.Persistence;
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
    string ItemNumber,
    string Description,
    DateOnly RenewalDate,
    decimal EstimatedAmount);

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
    int PendingApprovals,
    int RenewalsDueIn30Days,
    int BudgetItemCount,
    int ImportBatchesNeedingReview,
    IReadOnlyList<DashboardRenewalItem> UpcomingRenewals,
    IReadOnlyList<DashboardRecentBudgetItem> RecentBudgetItems);

public sealed class DashboardService(LedgerForgeDbContext dbContext)
{
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
                FiscalYearName = fiscalYear.DisplayName
            };
        }

        var items = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id && x.BudgetVersionId == version.Id)
            .Select(x => new
            {
                x.Id,
                x.ItemNumber,
                x.Description,
                x.Status,
                x.PlannedTotal,
                x.ApprovedTotal,
                x.RevisedTotal,
                x.RenewalDate,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var planned = items.Sum(x => x.PlannedTotal);
        var approved = items.Sum(x => x.ApprovedTotal ?? 0m);
        var revised = items.Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal);
        const decimal committed = 0m;
        const decimal actual = 0m;
        var available = revised - committed - actual;
        var forecast = revised;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var renewalCutoff = today.AddDays(30);
        var upcomingRenewals = items
            .Where(x => x.RenewalDate is not null && x.RenewalDate.Value >= today && x.RenewalDate.Value <= renewalCutoff)
            .OrderBy(x => x.RenewalDate)
            .Take(5)
            .Select(x => new DashboardRenewalItem(
                x.Id,
                x.ItemNumber,
                x.Description,
                x.RenewalDate!.Value,
                x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal))
            .ToArray();

        var recentItems = items
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
            items.Count(x => x.Status == BudgetItemStatus.Submitted),
            upcomingRenewals.Length,
            items.Count,
            await CountImportReviewAsync(cancellationToken),
            upcomingRenewals,
            recentItems);
    }

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
            0,
            0,
            0,
            importReviewCount,
            [],
            []);
}
