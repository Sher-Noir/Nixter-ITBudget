using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class RecurringActualPostingIntegrationTests
{
    [Fact]
    public async Task RecurringActuals_PostOnlyAfterDate_EditFutureAndCorrectPostedMonth_WhenSqlGateAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connectionString).Options;
        await using var db = new LedgerForgeDbContext(options);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fy = new FiscalYear($"Recurring {suffix}", new DateOnly(2039, 1, 1), new DateOnly(2039, 12, 31), 2039);
        fy.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(fy.Id, $"Recurring {suffix}", BudgetVersionType.InitialPlanning, 1, effectiveDate: fy.StartDate);
        var item = new BudgetItem(fy.Id, version.Id, $"REC-{suffix}", "1", "HYCU recurring test", 1m, 20000m);
        db.FiscalYears.Add(fy);
        db.BudgetVersions.Add(version);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync();

        var service = new RecurringActualService(db);
        var ruleId = await service.CreateAsync(item.Id, "HYCU", 1310.40m, true, 31,
            new DateOnly(2039, 1, 1), new DateOnly(2039, 3, 31), "HYCU-MONTHLY",
            null, null, null);

        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2039, 1, 31)));
        Assert.Empty(await db.ActualTransactions.Where(x => x.FiscalYearId == fy.Id).ToListAsync());

        Assert.Equal(1, await service.PostDueAsync(new DateOnly(2039, 2, 1)));
        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2039, 2, 1)));
        Assert.Equal(1, await db.RecurringActualOccurrences.CountAsync(x => x.ScheduleId == ruleId));
        var jan = await db.ActualTransactions.SingleAsync(x => x.FiscalYearId == fy.Id);
        Assert.Equal(new DateOnly(2039, 1, 31), jan.TransactionDate);
        Assert.Equal(1310.40m, jan.Amount);

        await service.UpdateAsync(item.Id, ruleId, "HYCU", 1410.40m, true, 31,
            new DateOnly(2039, 1, 1), new DateOnly(2039, 3, 31),
            "HYCU-MONTHLY", null, null, null);
        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2039, 2, 28)));
        Assert.Equal(1, await service.PostDueAsync(new DateOnly(2039, 3, 1)));
        Assert.Equal(2, await db.RecurringActualOccurrences.CountAsync(x => x.ScheduleId == ruleId));
        var feb = await db.ActualTransactions.SingleAsync(x => x.FiscalYearId == fy.Id && x.TransactionDate == new DateOnly(2039, 2, 28));
        Assert.Equal(1410.40m, feb.Amount);
        Assert.Equal(1310.40m, jan.Amount);
        Assert.Equal(2720.80m, await db.ActualTransactions.Where(x => x.FiscalYearId == fy.Id).SumAsync(x => x.Amount));

        await service.SetActiveAsync(item.Id, ruleId, false);
        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2039, 4, 1)));
        await service.SetActiveAsync(item.Id, ruleId, true);
        Assert.Equal(1, await service.PostDueAsync(new DateOnly(2039, 4, 1)));
        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2039, 4, 1)));
        Assert.Equal(3, await db.RecurringActualOccurrences.CountAsync(x => x.ScheduleId == ruleId));

        // Correct a posted month: only that month's amount changes in the ledger net.
        var ledger = new ActualLedgerService(db);
        await ledger.CorrectBudgetItemManualAsync(item.Id, jan.Id,
            new DateOnly(2039, 1, 31), 1299.95m, "HYCU January adjustment",
            "HYCU-MONTHLY", null, null, null, "Vendor price adjustment");
        db.ChangeTracker.Clear();
        var total = await db.ActualTransactions.Where(x => x.FiscalYearId == fy.Id)
            .SumAsync(x => x.Amount);
        Assert.Equal(1299.95m + 1410.40m + 1410.40m, total);
        Assert.Equal(20000m - total, 20000m - await db.ActualTransactions
            .Where(x => x.BudgetItemId == item.Id).SumAsync(x => x.Amount));
        Assert.Equal(3, await db.RecurringActualOccurrences.CountAsync(x => x.ScheduleId == ruleId));
    }

    [Fact]
    public async Task ClosedFiscalYear_PreventsBackfill_WhenSqlGateAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new LedgerForgeDbContext(options);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fy = new FiscalYear($"Close recurring {suffix}", new DateOnly(2040, 1, 1), new DateOnly(2040, 12, 31), 2040);
        fy.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(fy.Id, $"Close recurring {suffix}", BudgetVersionType.InitialPlanning, 1, effectiveDate: fy.StartDate);
        var item = new BudgetItem(fy.Id, version.Id, $"CLOSE-{suffix}", "1", "Closed year test", 1m, 1000m);
        db.FiscalYears.Add(fy);
        db.BudgetVersions.Add(version);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync();
        var service = new RecurringActualService(db);
        var id = await service.CreateAsync(item.Id, "Charge", 20m, true, 31,
            fy.StartDate, fy.EndDate, null, null, null, null);
        fy.Close(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        Assert.Equal(0, await service.PostDueAsync(new DateOnly(2041, 1, 1)));
        Assert.Equal(0, await db.RecurringActualOccurrences.CountAsync(x => x.ScheduleId == id));
    }
}
