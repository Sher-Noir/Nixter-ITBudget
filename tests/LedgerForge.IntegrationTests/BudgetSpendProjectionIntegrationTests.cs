using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

[Collection("RecurringActualPosting")]
public sealed class BudgetSpendProjectionIntegrationTests
{
    [Fact]
    public async Task Expected50000_Actual51000_DisplaysNegative1000_WhenSqlGateAvailable()
    {
        var conn = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(conn).Options;
        await using var db = new LedgerForgeDbContext(options);

        var today = RecurringActualService.EasternToday();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fy = new FiscalYear($"Variance {suffix}",
            new DateOnly(today.Year - 1, 1, 1),
            new DateOnly(today.Year + 1, 12, 31), today.Year);
        fy.SetStatus(FiscalYearStatus.Active);
        var ver = new BudgetVersion(fy.Id, $"Variance {suffix}",
            BudgetVersionType.InitialPlanning, 1, effectiveDate: fy.StartDate);
        var item = new BudgetItem(fy.Id, ver.Id, $"OVER-{suffix}", "1",
            "One-time expense", 1m, 50000m);
        db.FiscalYears.Add(fy);
        db.BudgetVersions.Add(ver);
        db.BudgetItems.Add(item);
        var tx = new ActualTransaction(fy.Id, today.AddDays(-1),
            51000m, "Invoice cost", ActualTransactionKind.Manual, budgetItemId: item.Id);
        db.ActualTransactions.Add(tx);
        await db.SaveChangesAsync();

        var actuals = new Dictionary<Guid, decimal> { [item.Id] = 51000m };
        var projection = await BudgetItemSpendProjectionQuery.GetAsync(
            db, fy.Id, ver.Id,
            [new BudgetItemSpendInput(item.Id, today.AddDays(-2), 50000m, item.Status)],
            actuals);
        Assert.Equal(50000m, projection[item.Id].ExpectedToDate);
        Assert.Equal(51000m, projection[item.Id].FullYearForecast);
        Assert.Equal(-1000m, projection[item.Id].ExpectedToDate - actuals[item.Id]);
    }

    [Fact]
    public async Task RecurringExpectedToDate_KeepsOriginalMonthPlanWhenCorrected_WhenSqlGateAvailable()
    {
        var conn = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn)) return;
        await using var db = new LedgerForgeDbContext(
            new DbContextOptionsBuilder<LedgerForgeDbContext>().UseSqlServer(conn).Options);

        var today = RecurringActualService.EasternToday();
        var priorMonth = new DateOnly(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1);
        var start = new DateOnly(priorMonth.Year, priorMonth.Month, 1);
        var nextMonth = today.AddMonths(1);
        var end = new DateOnly(nextMonth.Year, nextMonth.Month,
            DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fy = new FiscalYear($"Month {suffix}",
            new DateOnly(today.Year - 1, 1, 1),
            new DateOnly(today.Year + 1, 12, 31), today.Year);
        fy.SetStatus(FiscalYearStatus.Active);
        var ver = new BudgetVersion(fy.Id, $"Month {suffix}",
            BudgetVersionType.InitialPlanning, 1, effectiveDate: fy.StartDate);
        var item = new BudgetItem(fy.Id, ver.Id, $"MONTH-{suffix}", "1",
            "HYCU", 1m, 30000m);
        db.FiscalYears.Add(fy);
        db.BudgetVersions.Add(ver);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync();

        var service = new RecurringActualService(db);
        var id = await service.CreateAsync(item.Id, "HYCU", 1310.40m,
            true, 31, start, end, "HYCU-MONTHLY", null, null, null);
        Assert.Equal(1, await service.PostDueForScheduleAsync(id, today));
        var original = await db.ActualTransactions.AsNoTracking()
            .SingleAsync(x => x.BudgetItemId == item.Id);
        await new ActualLedgerService(db).CorrectBudgetItemManualAsync(
            item.Id, original.Id, original.TransactionDate,
            1375m, "HYCU adjusted month", "HYCU-MONTHLY",
            null, null, null, "Price changed this month");
        db.ChangeTracker.Clear();
        var actual = await db.ActualTransactions.AsNoTracking()
            .Where(x => x.BudgetItemId == item.Id).SumAsync(x => x.Amount);
        var forecast = await BudgetItemSpendProjectionQuery.GetAsync(
            db, fy.Id, ver.Id,
            [new BudgetItemSpendInput(item.Id, null, 30000m, item.Status)],
            new Dictionary<Guid, decimal> { [item.Id] = actual });
        Assert.Equal(1310.40m, forecast[item.Id].ExpectedToDate);
        Assert.Equal(1375m + 2620.80m, forecast[item.Id].FullYearForecast);
        Assert.Equal(-64.60m, forecast[item.Id].ExpectedToDate - actual);
    }
}
