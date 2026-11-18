using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetItemSpendProjection(decimal FullYearForecast, decimal ExpectedToDate);
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
            var expected = expectedByItem.GetValueOrDefault(x.Id);
            // One-time items with a dated purchase are expected after that date.
            // Without a purchase date, no reliable expected-to-date point exists.
            // Never double count a budget item that already has recurring schedules.
            if (!itemsWithSchedules.Contains(x.Id) && x.PurchaseDate is DateOnly purchaseDate
                && purchaseDate < today && x.Status is not BudgetItemStatus.Cancelled
                and not BudgetItemStatus.Archived)
                expected += x.CurrentBudget;

            var commitments = Math.Max(0m,
                po.GetValueOrDefault(x.Id) - invoice.GetValueOrDefault(x.Id));
            decimal? scenario = published.TryGetValue(x.Id, out var p) ? p : null;
            var annual = BudgetItemForecastCalculator.Calculate(
                actualTotals.GetValueOrDefault(x.Id), commitments,
                recurringByItem.GetValueOrDefault(x.Id), scenario).Total;
            return new BudgetItemSpendProjection(annual, expected);
        });
    }

    private sealed record PostedMonth(
        Guid ScheduleId, int Year, int Month, DateOnly Date, decimal OriginalAmount);
}
