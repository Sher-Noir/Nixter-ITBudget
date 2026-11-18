using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetItemRecurringForecastIntegrationTests
{
    [Fact]
    public async Task ItemForecast_MovesMonthFromProjectedToPostedWithoutDoubleCounting_WhenSqlGateAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new LedgerForgeDbContext(options);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"Forecast {suffix}", new DateOnly(2039, 1, 1),
            new DateOnly(2039, 12, 31), 2039);
        year.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(year.Id, $"Forecast {suffix}", BudgetVersionType.InitialPlanning, 1,
            effectiveDate: year.StartDate);
        var item = new BudgetItem(year.Id, version.Id, $"FORECAST-{suffix}", "1",
            "Forecast monthly license", 1m, 20000m);
        db.FiscalYears.Add(year);
        db.BudgetVersions.Add(version);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync();

        var recurring = new RecurringActualService(db);
        await recurring.CreateAsync(item.Id, "HYCU", 1310.40m, true, 31,
            new DateOnly(2039, 1, 1), new DateOnly(2039, 3, 31),
            "HYCU-MONTHLY", null, null, null);

        var years = new FiscalYearAdministrationService(db, new OutstandingCommitmentService(db));
        var planning = new BudgetPlanningService(db, years);
        var workspaceService = new BudgetItemWorkspaceService(db, planning);
        var before = await workspaceService.GetAsync(item.Id);
        Assert.NotNull(before);
        Assert.Equal(0m, before.Actual);
        Assert.Equal(3931.20m, before.ForecastDetails.UnpostedRecurring);
        Assert.Equal(3931.20m, before.Forecast);

        Assert.Equal(1, await recurring.PostDueAsync(new DateOnly(2039, 2, 1)));
        var after = await workspaceService.GetAsync(item.Id);
        Assert.NotNull(after);
        Assert.Equal(1310.40m, after.Actual);
        Assert.Equal(2620.80m, after.ForecastDetails.UnpostedRecurring);
        Assert.Equal(3931.20m, after.Forecast);

        await recurring.UpdateAsync(item.Id,
            (await recurring.ListForItemAsync(item.Id)).Single().Id,
            "HYCU", 1350.40m, true, 31, new DateOnly(2039, 1, 1),
            new DateOnly(2039, 3, 31), "HYCU-MONTHLY", null, null, null);
        db.ChangeTracker.Clear();
        var revised = await workspaceService.GetAsync(item.Id);
        Assert.NotNull(revised);
        Assert.Equal(1310.40m, revised.Actual);
        Assert.Equal(2700.80m, revised.ForecastDetails.UnpostedRecurring);
        Assert.Equal(4011.20m, revised.Forecast);
    }
}
