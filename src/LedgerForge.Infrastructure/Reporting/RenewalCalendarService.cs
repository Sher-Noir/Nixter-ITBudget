using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Reporting;

public enum RenewalTiming
{
    Overdue,
    DueWithin30Days,
    DueWithin90Days,
    Future
}

public sealed record RenewalCalendarItem(
    Guid BudgetItemId,
    string ItemNumber,
    string Description,
    DateOnly RenewalDate,
    int DaysUntilRenewal,
    RenewalTiming Timing,
    decimal EstimatedAmount,
    string Status);

public sealed record RenewalCalendarSnapshot(
    IReadOnlyList<ReportFiscalYearOption> FiscalYears,
    Guid? SelectedFiscalYearId,
    string FiscalYearName,
    IReadOnlyList<RenewalCalendarItem> Items,
    int OverdueCount,
    int DueWithin30DaysCount,
    int DueWithin90DaysCount,
    decimal DueWithin90DaysAmount);

public sealed class RenewalCalendarService(LedgerForgeDbContext dbContext)
{
    public async Task<RenewalCalendarSnapshot> GetAsync(Guid? fiscalYearId, CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new ReportFiscalYearOption(x.Id, x.DisplayName, x.IsCurrent))
            .ToListAsync(cancellationToken);

        var selectedId = fiscalYearId is not null && years.Any(x => x.Id == fiscalYearId.Value)
            ? fiscalYearId
            : years.FirstOrDefault(x => x.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
        if (selectedId is null)
            return new(years, null, "No fiscal year", [], 0, 0, 0, 0m);

        var versionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (versionId is null)
            return new(years, selectedId, years.Single(x => x.Id == selectedId.Value).Name, [], 0, 0, 0, 0m);

        var rows = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value && x.BudgetVersionId == versionId.Value && x.RenewalDate != null)
            .OrderBy(x => x.RenewalDate)
            .Select(x => new
            {
                x.Id,
                x.ItemNumber,
                x.Description,
                x.RenewalDate,
                x.RevisedTotal,
                x.ApprovedTotal,
                x.PlannedTotal,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = rows.Select(x =>
        {
            var renewalDate = x.RenewalDate!.Value;
            var days = renewalDate.DayNumber - today.DayNumber;
            var timing = days < 0
                ? RenewalTiming.Overdue
                : days <= 30
                    ? RenewalTiming.DueWithin30Days
                    : days <= 90
                        ? RenewalTiming.DueWithin90Days
                        : RenewalTiming.Future;

            return new RenewalCalendarItem(
                x.Id,
                x.ItemNumber,
                x.Description,
                renewalDate,
                days,
                timing,
                x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal,
                x.Status.ToString());
        }).ToArray();

        return new(
            years,
            selectedId,
            years.Single(x => x.Id == selectedId.Value).Name,
            items,
            items.Count(x => x.Timing == RenewalTiming.Overdue),
            items.Count(x => x.Timing == RenewalTiming.DueWithin30Days),
            items.Count(x => x.Timing is RenewalTiming.DueWithin30Days or RenewalTiming.DueWithin90Days),
            items.Where(x => x.Timing is RenewalTiming.DueWithin30Days or RenewalTiming.DueWithin90Days).Sum(x => x.EstimatedAmount));
    }
}
