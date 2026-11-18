using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

[Collection("RecurringActualPosting")]
public sealed class MonthlyBillingVarianceIntegrationTests
{
    [Fact]
    public async Task ManualMonthlyCharges_AutoDetectAndMatchFourBilledMonths_WhenSqlGateAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using var db = new LedgerForgeDbContext(
            new DbContextOptionsBuilder<LedgerForgeDbContext>()
                .UseSqlServer(connection).Options);

        var today = RecurringActualService.EasternToday();
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-4);
        var fiscalEnd = first.AddMonths(12).AddDays(-1);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fy = new FiscalYear($"Monthly {suffix}", first, fiscalEnd, first.Year);
        fy.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(fy.Id, $"Monthly {suffix}",
            BudgetVersionType.InitialPlanning, 1, effectiveDate: first);
        var ubeo = new BudgetItem(fy.Id, version.Id,
            $"UBEO-{suffix}", "1", "UBEO Financial Services", 1m, 43200m);
        var unscheduled = new BudgetItem(fy.Id, version.Id,
            $"SWITCH-{suffix}", "2", "Undated one-time equipment", 1m, 28000m);
        db.FiscalYears.Add(fy);
        db.BudgetVersions.Add(version);
        db.BudgetItems.AddRange(ubeo, unscheduled);

        var charges = new[] { 3989.02m, 3989.02m, 3989.02m, 3947.87m };
        for (var i = 0; i < charges.Length; i++)
        {
            var month = first.AddMonths(i);
            var booked = new DateOnly(month.Year, month.Month,
                DateTime.DaysInMonth(month.Year, month.Month));
            db.ActualTransactions.Add(new ActualTransaction(
                fy.Id, booked, charges[i], $"UBEO {month:yyyy-MM} monthly invoice",
                ActualTransactionKind.Manual, budgetItemId: ubeo.Id));
        }
        await db.SaveChangesAsync();

        var planning = new BudgetPlanningService(db,
            new FiscalYearAdministrationService(db,
                new OutstandingCommitmentService(db)));
        var snap = await planning.GetSnapshotAsync(fy.Id, version.Id);
        var line = Assert.Single(snap.Items.Where(x => x.Id == ubeo.Id));
        Assert.Equal(BudgetBillingCadence.Automatic, line.BillingCadence);
        Assert.Equal(BudgetBillingCadence.Monthly, line.EffectiveBillingCadence);
        Assert.Equal(4, line.BilledPeriods);
        Assert.Equal(3600m, line.BudgetPerBillingPeriod);
        Assert.Equal(14400m, line.ExpectedInstallmentsToDate);
        Assert.Equal(14400m, line.ExpectedToDate);
        Assert.Equal(15914.93m, line.Actual);
        Assert.Equal(-1514.93m, line.ExpectedToDate - line.Actual);
        Assert.Equal(28800m, line.RemainingInstallmentAllowance);
        Assert.Equal(44714.93m, line.Forecast);

        var switchItem = Assert.Single(snap.Items.Where(x => x.Id == unscheduled.Id));
        Assert.Equal(0m, switchItem.ExpectedToDate);
        Assert.Equal(0m, switchItem.Actual);
        Assert.Equal(28000m, switchItem.Forecast);
    }
}
