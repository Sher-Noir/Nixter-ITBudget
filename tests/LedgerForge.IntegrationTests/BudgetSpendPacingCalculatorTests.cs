using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetSpendPacingCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 4, 1);
    private static readonly DateOnly End = new(2027, 3, 31);
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void UndatedItemWithActual_UsesItsBudgetAsExpectedSpend()
    {
        var expected = BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, null, BudgetItemStatus.Active, 51000m, Start, End, Today);
        Assert.Equal(50000m, expected);
        Assert.Equal(-1000m, expected - 51000m);
    }

    [Fact]
    public void UndatedUnspentItem_HasNoAssumedBillingDate()
    {
        var expected = BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            36500m, null, BudgetItemStatus.Draft, 0m, Start, End, Today);
        Assert.Equal(0m, expected);
    }

    [Fact]
    public void EstimatedPurchaseDate_Alone_DoesNotCreateSpendingPaceSurplus()
    {
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, new DateOnly(2026, 12, 1), BudgetItemStatus.Approved, 0m,
            Start, End, Today));
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, Today, BudgetItemStatus.Approved, 0m,
            Start, End, Today));
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, Today.AddDays(-15), BudgetItemStatus.Approved, 0m,
            Start, End, Today));
        Assert.Equal(50000m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, Today.AddDays(-15), BudgetItemStatus.Purchased, 0m,
            Start, End, Today));
        Assert.Equal(50000m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, Today.AddDays(-15), BudgetItemStatus.Approved, 398m,
            Start, End, Today));
    }

    [Fact]
    public void UndatedUnspentItem_NeverInventsCalendarPacing()
    {
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            36500m, null, BudgetItemStatus.Draft, 0m, Start, End, Start.AddDays(-1)));
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            36500m, null, BudgetItemStatus.Draft, 0m, Start, End, End.AddDays(1)));
    }

    [Fact]
    public void PurchasedUndatedItem_UsesFullBudgetEvenWithoutActual()
    {
        Assert.Equal(50000m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, null, BudgetItemStatus.Purchased, 0m, Start, End, Today));
    }

    [Fact]
    public void CancelledItems_NotExpectedSpend()
    {
        Assert.Equal(0m, BudgetSpendPacingCalculator.ExpectedOneTimeToDate(
            50000m, null, BudgetItemStatus.Cancelled, 51000m, Start, End, Today));
    }
}
