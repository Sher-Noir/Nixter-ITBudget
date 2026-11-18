using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetItemSpendProjection(
    decimal FullYearForecast,
    decimal ExpectedToDate,
    decimal ExpectedRecurringToDate,
    decimal ExpectedDatedPurchaseToDate,
    decimal ExpectedUndatedSpentToDate,
    decimal ExpectedUndatedPacedToDate,
    decimal CurrentBudget,
    decimal UnpostedRecurring,
    decimal OutstandingCommitments,
    decimal AdditionalUncoveredCommitments,
    decimal? PublishedScenarioTotal,
    decimal? ForecastFloor,
    decimal AdditionalExpectedFromForecastFloor,
    string ExpectedToDateBasis,
    string RecurringScheduleDetails);
public sealed record BudgetItemSpendInput(Guid Id, DateOnly? PurchaseDate, decimal CurrentBudget, BudgetItemStatus Status);

/// <summary>Batch-computes consistent item-level annual forecast and expected spend to date.</summary>
public static class BudgetItemSpendProjectionQuery
{
    public static async Task<IReadOnlyDictionary<Guid, BudgetItemSpendProjection>> GetAsync(
        LedgerForgeDbContext db, Guid fiscalYearId, Guid versionId,
        IReadOnlyList<BudgetItemSpendInput> inputs,
        IReadOnlyDictionary<Guid, decimal> actualTotals, CancellationToken ct = default)
    {
        if (inputs.Count == 0) return new Dictionary<Guid, BudgetItemSpendProjection>();
        var fiscalYear = await db.FiscalYears.AsNoTracking()
            .Where(x => x.Id == fiscalYearId)
            .Select(x => new { x.StartDate, x.EndDate })
            .SingleAsync(ct);
        var ids = inputs.Select(x => x.Id).Distinct().ToArray();
        var poRows = await (
            from l in db.PurchaseOrderLines.AsNoTracking()
            join o in db.PurchaseOrders.AsNoTracking() on l.PurchaseOrderId equals o.Id
            where l.BudgetItemId != null && ids.Contains(l.BudgetItemId.Value) &&
                  !l.IsArchived && !o.IsArchived && o.State == PurchaseOrderState.Issued
            select new { ItemId = l.BudgetItemId!.Value, l.LineTotal }
        ).ToListAsync(ct);
        var invoiceRows = await (
            from a in db.InvoiceAllocations.AsNoTracking()
            join i in db.Invoices.AsNoTracking() on a.InvoiceId equals i.Id
            where a.BudgetItemId != null && ids.Contains(a.BudgetItemId.Value) &&
                  !a.IsArchived && !i.IsArchived && i.State == InvoiceState.Posted
            select new { ItemId = a.BudgetItemId!.Value, a.Amount }
        ).ToListAsync(ct);
        var po = poRows.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.LineTotal));
        var invoice = invoiceRows.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var schedules = await db.RecurringActualSchedules.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && ids.Contains(x.BudgetItemId))
            .ToListAsync(ct);
        var scheduleIds = schedules.Select(x => x.Id).ToArray();
        var posted = scheduleIds.Length == 0
            ? new List<PostedMonth>()
            : await (
                from m in db.RecurringActualOccurrences.AsNoTracking()
                join orig in db.ActualTransactions.AsNoTracking()
                    on m.ActualTransactionId equals orig.Id
                where scheduleIds.Contains(m.ScheduleId)
                select new PostedMonth(m.ScheduleId, m.BillingYear, m.BillingMonth,
                    m.BillingDate, orig.Amount)
            ).ToListAsync(ct);
        var postedKeys = posted.Select(x => (x.ScheduleId, x.Year, x.Month)).ToHashSet();
        var itemBySchedule = schedules.ToDictionary(x => x.Id, x => x.BudgetItemId);
        var itemsWithSchedules = schedules.Select(x => x.BudgetItemId).ToHashSet();
        var recurringByItem = new Dictionary<Guid, decimal>();
        var expectedByItem = new Dictionary<Guid, decimal>();
        var today = RecurringActualService.EasternToday();

        // Historical billed amounts remain at their original scheduled amount
        // even when a single month was later corrected or the future rule edited.
        foreach (var month in posted.Where(x => x.Date < today))
        {
            var id = itemBySchedule[month.ScheduleId];
            expectedByItem[id] = expectedByItem.GetValueOrDefault(id) + month.OriginalAmount;
        }
        foreach (var schedule in schedules.Where(x => x.IsActive))
        {
            var first = new DateOnly(schedule.StartDate.Year, schedule.StartDate.Month, 1);
            var last = new DateOnly(schedule.EndDate.Year, schedule.EndDate.Month, 1);
            for (var month = first; month <= last; month = month.AddMonths(1))
            {
                var due = schedule.BillingDate(month.Year, month.Month);
                if (due < schedule.StartDate || due > schedule.EndDate ||
                    postedKeys.Contains((schedule.Id, month.Year, month.Month))) continue;

                recurringByItem[schedule.BudgetItemId] =
                    recurringByItem.GetValueOrDefault(schedule.BudgetItemId) + schedule.Amount;
                // Align with posting: only billing dates already passed count as due today.
                if (due < today)
                    expectedByItem[schedule.BudgetItemId] =
                        expectedByItem.GetValueOrDefault(schedule.BudgetItemId) + schedule.Amount;
            }
        }

        var publishedId = await db.ForecastScenarios.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == versionId
                && x.State == ForecastScenarioState.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.AsOfDate)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var published = publishedId is null ? new Dictionary<Guid, decimal>() :
            await db.ForecastLines.AsNoTracking()
                .Where(x => x.ForecastScenarioId == publishedId.Value && ids.Contains(x.BudgetItemId))
                .ToDictionaryAsync(x => x.BudgetItemId, x => x.ForecastTotal, ct);

        return inputs.ToDictionary(x => x.Id, x =>
        {
            var actual = actualTotals.GetValueOrDefault(x.Id);
            var recurringExpected = expectedByItem.GetValueOrDefault(x.Id);
            var oneTimeExpected = itemsWithSchedules.Contains(x.Id) ? 0m :
                BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
                    x.CurrentBudget, x.PurchaseDate, x.Status, actual,
                    fiscalYear.StartDate, fiscalYear.EndDate, today);

            var datedPurchase = x.PurchaseDate is not null ? oneTimeExpected : 0m;
            var undatedSpent = x.PurchaseDate is null &&
                (x.Status == BudgetItemStatus.Purchased || actual > 0m)
                ? oneTimeExpected : 0m;
            var undatedPaced = x.PurchaseDate is null &&
                x.Status != BudgetItemStatus.Purchased && actual <= 0m
                ? oneTimeExpected : 0m;
            if (itemsWithSchedules.Contains(x.Id))
            {
                datedPurchase = 0m;
                undatedSpent = 0m;
                undatedPaced = 0m;
            }

            var commitments = Math.Max(0m,
                po.GetValueOrDefault(x.Id) - invoice.GetValueOrDefault(x.Id));
            decimal? publishedScenario = published.TryGetValue(x.Id, out var p) ? p : null;
            // When no recurring rule exists, the current planned/approved/revised
            // line amount is still expected full-year spend unless cancelled.
            // Otherwise the annual forecast would omit all unspent budget lines.
            decimal? scenarioOrPlan = itemsWithSchedules.Contains(x.Id)
                ? publishedScenario
                : x.Status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived
                    ? publishedScenario
                    : Math.Max(publishedScenario ?? 0m, x.CurrentBudget);
            var remainingRecurring = recurringByItem.GetValueOrDefault(x.Id);
            var breakdown = BudgetItemForecastCalculator.Calculate(
                actual, commitments, remainingRecurring, scenarioOrPlan);
            var itemSchedules = schedules.Where(s => s.BudgetItemId == x.Id).ToArray();
            var basis = itemSchedules.Length > 0
                ? "Recurring schedule (one-time estimate excluded)"
                : x.Status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived
                    ? "Excluded (cancelled or archived)"
                    : x.PurchaseDate is DateOnly
                        ? "Estimated purchase date (full budget after date)"
                        : x.Status == BudgetItemStatus.Purchased || actual > 0m
                            ? "Undated; purchased or actual posted (full current budget)"
                            : "Undated; estimated evenly across fiscal-year days";
            var scheduleDetails = string.Join("; ", itemSchedules.Select(s =>
                s.Description + " | " + s.Amount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) +
                " | " + (s.LastDayOfMonth ? "Last calendar day" : "Day " + s.DayOfMonth) +
                " | " + s.StartDate.ToString("yyyy-MM-dd") +
                " to " + s.EndDate.ToString("yyyy-MM-dd") +
                " | " + (s.IsActive ? "Active" : "Paused")));
            return new BudgetItemSpendProjection(
                breakdown.Total, recurringExpected + oneTimeExpected,
                recurringExpected, datedPurchase, undatedSpent, undatedPaced,
                x.CurrentBudget, breakdown.UnpostedRecurring,
                breakdown.OutstandingCommitments, breakdown.AdditionalUncoveredCommitments,
                publishedScenario, scenarioOrPlan, breakdown.OtherExpectedFromPublishedScenario,
                basis, scheduleDetails);
        });
    }

    private sealed record PostedMonth(
        Guid ScheduleId, int Year, int Month, DateOnly Date, decimal OriginalAmount);
}
