using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

[Collection("RecurringActualPosting")]
public sealed class BudgetOverUnderExclusionIntegrationTests
{
    [Fact]
    public void ExcludedItems_ContributeNeitherActualNorExpectedToVariance_ButKeepGrossActualAndForecast()
    {
        var included = BudgetOverUnderCalculator.Calculate(14400m, 15914.93m, false);
        var excluded = BudgetOverUnderCalculator.Calculate(10000m, 412.50m, true);
        Assert.Equal(-1514.93m, included.Variance);
        Assert.Equal(0m, excluded.Variance);
        Assert.Equal(0m, excluded.ActualIncluded);
        Assert.Equal(412.50m, excluded.ActualExcluded);
        Assert.Equal(10000m, excluded.ExpectedExcluded);
        Assert.Equal(-1514.93m, included.Variance + excluded.Variance);
        Assert.Equal(16327.43m, 15914.93m + excluded.ActualExcluded);
    }

    [Fact]
    public async Task SettingExclusion_PersistsWithoutChangingActualOrForecast_WhenSqlGateAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;

        await using var db = new LedgerForgeDbContext(
            new DbContextOptionsBuilder<LedgerForgeDbContext>().UseSqlServer(connection).Options);

        var today = LedgerForge.Infrastructure.Actuals.RecurringActualService.EasternToday();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"Exclusion {suffix}", today.AddDays(-30), today.AddDays(120), today.Year);
        year.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(year.Id, $"Exclusion {suffix}",
            BudgetVersionType.InitialPlanning, 1, effectiveDate: year.StartDate);
        var asNeeded = new BudgetItem(year.Id, version.Id,
            $"AS-NEEDED-{suffix}", "1", "Office supplies", 1m, 1000m);
        db.FiscalYears.Add(year);
        db.BudgetVersions.Add(version);
        db.BudgetItems.Add(asNeeded);
        db.ActualTransactions.Add(new ActualTransaction(
            year.Id, today.AddDays(-1), 325m, "Purchased supplies",
            ActualTransactionKind.Manual, budgetItemId: asNeeded.Id));
        await db.SaveChangesAsync();

        var planning = new BudgetPlanningService(db, new FiscalYearAdministrationService(
            db, new OutstandingCommitmentService(db)));

        var initial = await planning.GetSnapshotAsync(year.Id, version.Id);
        var first = Assert.Single(initial.Items);
        Assert.False(first.ExcludeFromOverUnder);
        Assert.Equal(325m, first.Actual);
        var startingForecast = first.Forecast;

        await planning.SetOverUnderExclusionAsync(asNeeded.Id, true);
        db.ChangeTracker.Clear();

        var after = await planning.GetSnapshotAsync(year.Id, version.Id);
        var item = Assert.Single(after.Items);
        Assert.True(item.ExcludeFromOverUnder);
        Assert.Equal(325m, item.Actual);
        Assert.Equal(startingForecast, item.Forecast);
        Assert.Equal(0m, BudgetOverUnderCalculator.Calculate(
            item.ExpectedToDate, item.Actual, item.ExcludeFromOverUnder).Variance);

        await planning.SetOverUnderExclusionAsync(asNeeded.Id, false);
        db.ChangeTracker.Clear();
        var restored = Assert.Single((await planning.GetSnapshotAsync(year.Id, version.Id)).Items);
        Assert.False(restored.ExcludeFromOverUnder);
        Assert.Equal(1000m - 325m, BudgetOverUnderCalculator.Calculate(
            restored.ExpectedToDate, restored.Actual, restored.ExcludeFromOverUnder).Variance);
    }

    [Fact]
    public void DatedUnbilledSoftware_IsNotExpectedToHaveBeenSpent()
    {
        var today = new DateOnly(2026, 11, 18);
        var start = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2027, 6, 30);
        // 1Password / SnagIT can be planned but not yet purchased or billed.
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            576m, today.AddMonths(-2), BudgetItemStatus.Approved, 0m,
            start, end, today));
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            69m, today.AddMonths(-1), BudgetItemStatus.Approved, 0m,
            start, end, today));
    }
}
