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
    string RecurringScheduleDetails,
    BudgetBillingCadence EffectiveBillingCadence,
    int BilledPeriods,
    decimal BudgetPerBillingPeriod,
    decimal ExpectedInstallmentsToDate,
    decimal RemainingInstallmentAllowance,
    string BillingCadenceBasis);
public sealed record BudgetItemSpendInput(
    Guid Id, DateOnly? PurchaseDate, decimal CurrentBudget, BudgetItemStatus Status,
    BudgetBillingCadence BillingCadence = BudgetBillingCadence.Automatic);

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

        // Read actual transaction dates once. Net reversals against the same
        // month and count each billed month only once (even for split invoices).
        var localToday = RecurringActualService.EasternToday();
        var transactionRows = await db.ActualTransactions.AsNoTracking()
            .Where(x => x.BudgetItemId != null && ids.Contains(x.BudgetItemId.Value)
                && x.TransactionDate >= fiscalYear.StartDate && x.TransactionDate <= localToday
                && x.TransactionDate <= fiscalYear.EndDate)
            .Select(x => new { BudgetItemId = x.BudgetItemId!.Value, x.TransactionDate, x.Amount })
            .ToListAsync(ct);
        var billedMonths = transactionRows
            .GroupBy(x => new { x.BudgetItemId, x.TransactionDate.Year, x.TransactionDate.Month })
            .Where(g => g.Sum(row => row.Amount) > 0m)
            .GroupBy(g => g.Key.BudgetItemId)
            .ToDictionary(g => g.Key, g => g
                .Select(x => (x.Key.Year * 12 + x.Key.Month - 1)).OrderBy(x => x).ToArray());
        var fiscalStartMonth = fiscalYear.StartDate.Year * 12 + fiscalYear.StartDate.Month - 1;
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
            var months = billedMonths.GetValueOrDefault(x.Id) ?? [];
            var billingPace = BudgetBillingPaceCalculator.Calculate(
                x.CurrentBudget, x.BillingCadence, months,
                months.Select(month => (month - fiscalStartMonth) / 3).Distinct().ToArray());
            var usesInstallments = !itemsWithSchedules.Contains(x.Id) &&
                (billingPace.EffectiveCadence is BudgetBillingCadence.Monthly or BudgetBillingCadence.Quarterly);
            var installmentExpected = usesInstallments ? billingPace.ExpectedToDate : 0m;
            var oneTimeExpected = itemsWithSchedules.Contains(x.Id) || usesInstallments ? 0m :
                BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
                    x.CurrentBudget, x.PurchaseDate, x.Status, actual,
                    fiscalYear.StartDate, fiscalYear.EndDate, today);

            var datedPurchase = x.PurchaseDate is not null ? oneTimeExpected : 0m;
            var undatedSpent = x.PurchaseDate is null &&
                (x.Status == BudgetItemStatus.Purchased || actual > 0m)
                ? oneTimeExpected : 0m;
            var undatedPaced = 0m; // no false fiscal-day proration
            if (itemsWithSchedules.Contains(x.Id) || usesInstallments)
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
            var remainingRecurring = recurringByItem.GetValueOrDefault(x.Id) +
                (usesInstallments ? billingPace.RemainingFullYearAllowance : 0m);
            var breakdown = BudgetItemForecastCalculator.Calculate(
                actual, commitments, remainingRecurring, scenarioOrPlan);
            var itemSchedules = schedules.Where(s => s.BudgetItemId == x.Id).ToArray();
            var basis = itemSchedules.Length > 0
                ? "Recurring schedule (one-time estimate excluded)"
                : usesInstallments
                    ? $"{billingPace.Basis}: {billingPace.BilledPeriods} billed periods × {billingPace.BudgetPerPeriod:C2}"
                : x.Status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived
                    ? "Excluded (cancelled or archived)"
                    : x.PurchaseDate is DateOnly
                        ? "One-time: expected at purchase date"
                        : x.Status == BudgetItemStatus.Purchased || actual > 0m
                            ? "One-time: undated, full budget expected once purchased"
                            : "Unscheduled one-time: no expected spending assumed";
            var scheduleDetails = string.Join("; ", itemSchedules.Select(s =>
                s.Description + " | " + s.Amount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) +
                " | " + (s.LastDayOfMonth ? "Last calendar day" : "Day " + s.DayOfMonth) +
                " | " + s.StartDate.ToString("yyyy-MM-dd") +
                " to " + s.EndDate.ToString("yyyy-MM-dd") +
                " | " + (s.IsActive ? "Active" : "Paused")));
            return new BudgetItemSpendProjection(
                breakdown.Total, recurringExpected + oneTimeExpected + installmentExpected,
                recurringExpected, datedPurchase, undatedSpent, undatedPaced,
                x.CurrentBudget, breakdown.UnpostedRecurring,
                breakdown.OutstandingCommitments, breakdown.AdditionalUncoveredCommitments,
                publishedScenario, scenarioOrPlan, breakdown.OtherExpectedFromPublishedScenario,
                basis, scheduleDetails,
                billingPace.EffectiveCadence,
                usesInstallments ? billingPace.BilledPeriods : 0,
                usesInstallments ? billingPace.BudgetPerPeriod : 0m,
                installmentExpected,
                usesInstallments ? billingPace.RemainingFullYearAllowance : 0m,
                itemSchedules.Length > 0 ? "Recurring actual schedule controls billing" : billingPace.Basis);
        });
    }

    private sealed record PostedMonth(
        Guid ScheduleId, int Year, int Month, DateOnly Date, decimal OriginalAmount);
}
