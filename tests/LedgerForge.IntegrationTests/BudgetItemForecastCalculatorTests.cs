using LedgerForge.Infrastructure.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetItemForecastCalculatorTests
{
    [Fact]
    public void RecurringRemaining_IsForecastedWithoutAddingItToPostedActual()
    {
        var forecast = BudgetItemForecastCalculator.Calculate(
            actual: 7862.40m, outstandingCommitments: 0m,
            unpostedRecurring: 7862.40m, publishedScenarioTotal: null);
        Assert.Equal(7862.40m, forecast.PostedActual);
        Assert.Equal(7862.40m, forecast.UnpostedRecurring);
        Assert.Equal(15724.80m, forecast.Total);
    }

    [Fact]
    public void PurchaseOrders_CoverRecurringCostsBeforeAddingExtraCommitments()
    {
        var forecast = BudgetItemForecastCalculator.Calculate(
            500m, 3000m, 2000m, null);
        Assert.Equal(1000m, forecast.AdditionalUncoveredCommitments);
        Assert.Equal(3500m, forecast.Total);
    }

    [Fact]
    public void PublishedForecast_IsNotAddedTwice()
    {
        var forecast = BudgetItemForecastCalculator.Calculate(
            500m, 100m, 200m, 2000m);
        Assert.Equal(1300m, forecast.OtherExpectedFromPublishedScenario);
        Assert.Equal(2000m, forecast.Total);
    }

    [Fact]
    public void PublishedScenarioLowerThanSupportedSpend_DoesNotHideActualAndRecurring()
    {
        var forecast = BudgetItemForecastCalculator.Calculate(500m, 100m, 200m, 400m);
        Assert.Equal(0m, forecast.OtherExpectedFromPublishedScenario);
        Assert.Equal(700m, forecast.Total);
    }

    [Fact]
    public void NoSpend_ForecastIsZeroNotAutomaticallyTheApprovedBudget()
    {
        var forecast = BudgetItemForecastCalculator.Calculate(0m, 0m, 0m, null);
        Assert.Equal(0m, forecast.Total);
    }
}
