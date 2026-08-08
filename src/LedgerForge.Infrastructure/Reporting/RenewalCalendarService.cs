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
    Guid Id,
    string Source,
    Guid? BudgetItemId,
    Guid? ContractId,
    string Reference,
    string Description,
    string? Vendor,
    DateOnly RenewalDate,
    DateOnly? NoticeDate,
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

public sealed class RenewalCalendarService(
    LedgerForgeDbContext dbContext,
    RenewalProjectionService renewalProjectionService)
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

        var rows = await renewalProjectionService.GetForFiscalYearAsync(selectedId.Value, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = rows.Select(x =>
        {
            var days = x.RenewalDate.DayNumber - today.DayNumber;
            var timing = days < 0
                ? RenewalTiming.Overdue
                : days <= 30
                    ? RenewalTiming.DueWithin30Days
                    : days <= 90
                        ? RenewalTiming.DueWithin90Days
                        : RenewalTiming.Future;

            return new RenewalCalendarItem(
                x.Id,
                x.Source,
                x.BudgetItemId,
                x.ContractId,
                x.Reference,
                x.Description,
                x.Vendor,
                x.RenewalDate,
                x.NoticeDate,
                days,
                timing,
                x.EstimatedAmount,
                x.Status);
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
