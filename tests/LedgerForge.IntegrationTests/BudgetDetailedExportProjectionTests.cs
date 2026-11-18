using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

[Collection("RecurringActualPosting")]
public sealed class BudgetDetailedExportProjectionTests
{
    [Fact]
    public async Task SelectedBudgetVersionAndFilteredFinancialTotals_ReconcileToBudgetSummary_WhenSqlGateAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;

        await using var db = new LedgerForgeDbContext(
            new DbContextOptionsBuilder<LedgerForgeDbContext>().UseSqlServer(connection).Options);

        var today = LedgerForge.Infrastructure.Actuals.RecurringActualService.EasternToday();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"Export {suffix}", today.AddDays(-30),
            today.AddDays(120), today.Year);
        year.SetStatus(FiscalYearStatus.Active);
        var older = new BudgetVersion(year.Id, $"Older {suffix}",
            BudgetVersionType.InitialPlanning, 1, effectiveDate: year.StartDate);
        var selected = new BudgetVersion(year.Id, $"Selected {suffix}",
            BudgetVersionType.InitialPlanning, 2, effectiveDate: year.StartDate);
        var target = new BudgetItem(year.Id, selected.Id, $"TARGET-{suffix}", "1",
            "HYCU subscription", 1m, 1310.40m);
        var other = new BudgetItem(year.Id, selected.Id, $"OTHER-{suffix}", "2",
            "Unrelated office supplies", 1m, 100m);
        var previous = new BudgetItem(year.Id, older.Id, $"OLD-{suffix}", "3",
            "HYCU older version", 1m, 9999m);
        db.FiscalYears.Add(year);
        db.BudgetVersions.AddRange(older, selected);
        db.BudgetItems.AddRange(target, other, previous);
        db.ActualTransactions.Add(new ActualTransaction(
            year.Id, today.AddDays(-2), 1410.40m, "Vendor charged above expected",
            ActualTransactionKind.Manual, budgetItemId: target.Id));
        await db.SaveChangesAsync();

        var years = new FiscalYearAdministrationService(
            db, new OutstandingCommitmentService(db));
        var planning = new BudgetPlanningService(db, years);
        var snapshot = await planning.GetSnapshotAsync(year.Id, selected.Id);
        Assert.Equal(selected.Id, snapshot.SelectedVersionId);
        Assert.Equal(2, snapshot.Items.Count);

        var filtered = BudgetItemFilters.Apply(snapshot.Items,
            new BudgetItemFilterOptions(null, null, null, null, null, null, "hycu"));
        var item = Assert.Single(filtered);
        Assert.Equal(target.Id, item.Id);
        Assert.Equal(1310.40m, item.ExpectedToDate);
        Assert.Equal(1410.40m, item.Actual);
        Assert.Equal(-100m, item.ExpectedToDate - item.Actual);
        Assert.Equal(1410.40m, item.Forecast);
        Assert.Equal(1310.40m, filtered.Sum(x => x.ExpectedToDate));
        Assert.Equal(1410.40m, filtered.Sum(x => x.Actual));
        Assert.Equal(-100m, filtered.Sum(x => x.ExpectedToDate - x.Actual));
        Assert.Equal(1310.40m, filtered.Sum(x => x.ExpectedUndatedSpentToDate));
        Assert.Equal(0m, filtered.Sum(x => x.ExpectedUndatedPacedToDate));
        Assert.Contains("undated", item.ExpectedToDateBasis, StringComparison.OrdinalIgnoreCase);
    }
}
