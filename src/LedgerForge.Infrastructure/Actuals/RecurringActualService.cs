using System.Data;
using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Actuals;

/// <summary>Creates and processes recurring monthly actuals. Future months are never actuals.</summary>
public sealed class RecurringActualService(LedgerForgeDbContext db)
{
    private static readonly TimeZoneInfo BillingTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static DateOnly EasternToday(DateTimeOffset? instant = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant ?? DateTimeOffset.UtcNow, BillingTimeZone).Date);

    public async Task<(DateOnly Start, DateOnly End)> GetFiscalDatesAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await db.BudgetItems.AsNoTracking().SingleAsync(x => x.Id == itemId, ct);
        var year = await db.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == item.FiscalYearId, ct);
        return (year.StartDate, year.EndDate);
    }

    public async Task<IReadOnlyList<RecurringActualSchedule>> ListForItemAsync(
        Guid itemId, CancellationToken ct = default) =>
        await db.RecurringActualSchedules.AsNoTracking()
            .Where(x => x.BudgetItemId == itemId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Description)
            .ToListAsync(ct);

    public async Task<Guid> CreateAsync(
        Guid itemId, string description, decimal amount, bool lastDayOfMonth,
        int dayOfMonth, DateOnly startDate, DateOnly endDate, string? sourceReference,
        Guid? financeAccountId, Guid? departmentId, Guid? locationId,
        CancellationToken ct = default)
    {
        var (item, year) = await GetOpenItemYearAsync(itemId, ct);
        ValidateDates(startDate, endDate, year);
        await ValidateLookups(financeAccountId, departmentId, locationId, ct);
        var schedule = new RecurringActualSchedule(
            year.Id, item.Id, description, amount, lastDayOfMonth, dayOfMonth,
            startDate, endDate, sourceReference, financeAccountId, departmentId, locationId);
        db.RecurringActualSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return schedule.Id;
    }

    public async Task UpdateAsync(
        Guid itemId, Guid scheduleId, string description, decimal amount,
        bool lastDayOfMonth, int dayOfMonth, DateOnly startDate, DateOnly endDate,
        string? sourceReference, Guid? financeAccountId, Guid? departmentId,
        Guid? locationId, CancellationToken ct = default)
    {
        var (_, year) = await GetOpenItemYearAsync(itemId, ct);
        var schedule = await GetScheduleAsync(itemId, scheduleId, ct);
        ValidateDates(startDate, endDate, year);
        await ValidateLookups(financeAccountId, departmentId, locationId, ct);
        schedule.Update(description, amount, lastDayOfMonth, dayOfMonth, startDate,
            endDate, sourceReference, financeAccountId, departmentId, locationId);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(
        Guid itemId, Guid scheduleId, bool active, CancellationToken ct = default)
    {
        _ = await GetOpenItemYearAsync(itemId, ct);
        var schedule = await GetScheduleAsync(itemId, scheduleId, ct);
        schedule.SetActive(active);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// At startup and hourly, backfills only already-passed bill dates.
    /// Each billing month is claimed once, even after schedule edits or restarts.
    /// </summary>
    public Task<int> PostDueForScheduleAsync(
        Guid scheduleId, DateOnly localToday, CancellationToken ct = default) =>
        PostDueAsync(localToday, ct, scheduleId);

    public async Task<int> PostDueAsync(
        DateOnly localToday, CancellationToken ct = default, Guid? scheduleFilterId = null)
    {
        var ids = await db.RecurringActualSchedules.AsNoTracking()
            .Where(x => x.IsActive && x.StartDate < localToday &&
                (scheduleFilterId == null || x.Id == scheduleFilterId.Value))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var posted = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var schedule = await db.RecurringActualSchedules.AsNoTracking()
                .SingleAsync(x => x.Id == id, ct);
            for (var month = new DateOnly(schedule.StartDate.Year, schedule.StartDate.Month, 1);
                 month < localToday && month <= schedule.EndDate;
                 month = month.AddMonths(1))
            {
                if (!schedule.Due(month.Year, month.Month, localToday)) continue;
                if (await PostOneAsync(schedule.Id, month.Year, month.Month, localToday, ct))
                    posted++;
            }
        }
        return posted;
    }

    private async Task<bool> PostOneAsync(
        Guid scheduleId, int year, int month, DateOnly localToday, CancellationToken ct)
    {
        // Guard against multiple IIS worker processes and manual reruns.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        try
        {
            var schedule = await db.RecurringActualSchedules.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == scheduleId, ct);
            if (schedule is null || !schedule.Due(year, month, localToday)) return false;

            var fiscalYear = await db.FiscalYears.AsNoTracking()
                .SingleAsync(x => x.Id == schedule.FiscalYearId, ct);
            if (fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
                return false;
            var date = schedule.BillingDate(year, month);
            if (date < fiscalYear.StartDate || date > fiscalYear.EndDate)
                return false;
            var itemExists = await db.BudgetItems.AsNoTracking().AnyAsync(
                x => x.Id == schedule.BudgetItemId && x.FiscalYearId == fiscalYear.Id &&
                     x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived, ct);
            if (!itemExists) return false;

            if (await db.RecurringActualOccurrences.AnyAsync(
                x => x.ScheduleId == scheduleId && x.BillingYear == year && x.BillingMonth == month, ct))
                return false;

            // Keep the actual and billing marker in one database transaction.
            // Existing corrections reverse/repost an individual month without modifying this rule.
            var actual = new ActualTransaction(
                schedule.FiscalYearId, date, schedule.Amount, schedule.Description,
                ActualTransactionKind.Manual, schedule.SourceReference, schedule.BudgetItemId,
                schedule.FinanceAccountId, schedule.DepartmentId, schedule.LocationId);
            db.ActualTransactions.Add(actual);
            db.RecurringActualOccurrences.Add(new RecurringActualOccurrence(
                schedule.Id, actual.Id, year, month, date));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        finally
        {
            // On early return an uncommitted transaction is disposed/rolled back.
        }
    }

    private async Task<RecurringActualSchedule> GetScheduleAsync(
        Guid itemId, Guid scheduleId, CancellationToken ct)
    {
        return await db.RecurringActualSchedules
            .SingleOrDefaultAsync(x => x.Id == scheduleId && x.BudgetItemId == itemId, ct)
            ?? throw new KeyNotFoundException("Recurring schedule not found.");
    }

    private async Task<(BudgetItem Item, FiscalYear Year)> GetOpenItemYearAsync(
        Guid itemId, CancellationToken ct)
    {
        var item = await db.BudgetItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == itemId, ct)
            ?? throw new KeyNotFoundException("Budget item not found.");
        if (item.Status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived)
            throw new InvalidOperationException("Cannot schedule actuals for cancelled or archived items.");
        var year = await db.FiscalYears.AsNoTracking()
            .SingleAsync(x => x.Id == item.FiscalYearId, ct);
        if (year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Cannot change recurring actuals for a closed or archived fiscal year.");
        return (item, year);
    }

    private static void ValidateDates(DateOnly start, DateOnly end, FiscalYear year)
    {
        if (start < year.StartDate || start > year.EndDate || end < start || end > year.EndDate)
            throw new ArgumentException(
                $"Schedule dates must fall within {year.DisplayName} ({year.StartDate:yyyy-MM-dd} to {year.EndDate:yyyy-MM-dd}).");
    }

    private async Task ValidateLookups(
        Guid? account, Guid? department, Guid? location, CancellationToken ct)
    {
        if (account is not null && !await db.FinanceAccounts.AsNoTracking().AnyAsync(x => x.Id == account && x.IsActive, ct))
            throw new InvalidOperationException("Finance account is not active.");
        if (department is not null && !await db.Departments.AsNoTracking().AnyAsync(x => x.Id == department && x.IsActive, ct))
            throw new InvalidOperationException("Department is not active.");
        if (location is not null && !await db.Locations.AsNoTracking().AnyAsync(x => x.Id == location && x.IsActive, ct))
            throw new InvalidOperationException("Location is not active.");
    }
}
