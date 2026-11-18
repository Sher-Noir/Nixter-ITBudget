using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetBillingPaceCalculatorTests
{
    [Fact]
    public void Ubeo_JulyThroughOctober_Is1514_93OverFourMonthlyAllowances()
    {
        var july = 2026 * 12 + 6;
        var months = new[] { july, july + 1, july + 2, july + 3 };
        var billings = new[] { 3989.02m, 3989.02m, 3989.02m, 3947.87m };
        var actual = billings.Sum();
        var pace = BudgetBillingPaceCalculator.Calculate(
            43200m, BudgetBillingCadence.Automatic, months, [0, 1]);

        Assert.Equal(BudgetBillingCadence.Monthly, pace.EffectiveCadence);
        Assert.Equal(4, pace.BilledPeriods);
        Assert.Equal(3600m, pace.BudgetPerPeriod);
        Assert.Equal(14400m, pace.ExpectedToDate);
        Assert.Equal(15914.93m, actual);
        Assert.Equal(-1514.93m, pace.ExpectedToDate - actual);
        Assert.Equal(28800m, pace.RemainingFullYearAllowance);
        Assert.Equal(44714.93m, actual + pace.RemainingFullYearAllowance);
    }

    [Fact]
    public void Automatic_RequiresThreeConsecutivePositiveBillingMonths()
    {
        var july = 2026 * 12 + 6;
        Assert.Equal(BudgetBillingCadence.OneTime,
            BudgetBillingPaceCalculator.Resolve(BudgetBillingCadence.Automatic,
                [july, july + 2, july + 4]));
        Assert.Equal(BudgetBillingCadence.OneTime,
            BudgetBillingPaceCalculator.Resolve(BudgetBillingCadence.Automatic,
                [july, july + 1]));
        Assert.Equal(BudgetBillingCadence.Monthly,
            BudgetBillingPaceCalculator.Resolve(BudgetBillingCadence.Automatic,
                [july, july + 1, july + 2]));
    }

    [Fact]
    public void ExplicitMonthly_CanCompareTheVeryFirstMonth()
    {
        var pace = BudgetBillingPaceCalculator.Calculate(
            43200m, BudgetBillingCadence.Monthly, [123], [0]);
        Assert.Equal(1, pace.BilledPeriods);
        Assert.Equal(3600m, pace.ExpectedToDate);
    }

    [Fact]
    public void Quarterly_CountsOneBudgetAllowancePerPostedQuarter()
    {
        var pace = BudgetBillingPaceCalculator.Calculate(
            40000m, BudgetBillingCadence.Quarterly, [1, 2, 4], [0, 1]);
        Assert.Equal(BudgetBillingCadence.Quarterly, pace.EffectiveCadence);
        Assert.Equal(2, pace.BilledPeriods);
        Assert.Equal(10000m, pace.BudgetPerPeriod);
        Assert.Equal(20000m, pace.ExpectedToDate);
        Assert.Equal(20000m, pace.RemainingFullYearAllowance);
    }

    [Fact]
    public void AnAnnualBudget_WithNoDatedBills_IsNotProrated()
    {
        var pace = BudgetBillingPaceCalculator.Calculate(
            60000m, BudgetBillingCadence.Automatic, [], []);
        Assert.Equal(BudgetBillingCadence.OneTime, pace.EffectiveCadence);
        Assert.Equal(0m, pace.ExpectedToDate);
    }
}
