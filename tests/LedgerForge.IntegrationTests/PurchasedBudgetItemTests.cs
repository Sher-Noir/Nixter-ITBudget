using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class PurchasedBudgetItemTests
{
    [Fact]
    public void Purchased_AppendedToEnumWithoutChangingExistingPersistedValues()
    {
        Assert.Equal(0, (int)BudgetItemStatus.Draft);
        Assert.Equal(3, (int)BudgetItemStatus.Approved);
        Assert.Equal(8, (int)BudgetItemStatus.Archived);
        Assert.Equal(9, (int)BudgetItemStatus.Purchased);
    }

    [Theory]
    [InlineData(BudgetItemStatus.Draft)]
    [InlineData(BudgetItemStatus.Proposed)]
    [InlineData(BudgetItemStatus.Deferred)]
    [InlineData(BudgetItemStatus.Approved)]
    [InlineData(BudgetItemStatus.Active)]
    public void MarkPurchased_PreservesApprovedCostAndPlanningTotals(BudgetItemStatus originalStatus)
    {
        var item = NewItem();
        item.SetStatus(originalStatus);
        item.SetApprovedTotal(56m);
        item.MarkPurchased();

        Assert.Equal(BudgetItemStatus.Purchased, item.Status);
        Assert.Equal(56m, item.ApprovedTotal);
        Assert.Equal(50m, item.PlannedTotal);
        Assert.False(item.IsPlanningEditable);
    }

    [Theory]
    [InlineData(BudgetItemStatus.Submitted)]
    [InlineData(BudgetItemStatus.Denied)]
    [InlineData(BudgetItemStatus.Cancelled)]
    [InlineData(BudgetItemStatus.Archived)]
    [InlineData(BudgetItemStatus.Purchased)]
    public void MarkPurchased_RejectsInvalidTransitions(BudgetItemStatus originalStatus)
    {
        var item = NewItem();
        item.SetStatus(originalStatus);
        Assert.Throws<InvalidOperationException>(() => item.MarkPurchased());
        Assert.Equal(originalStatus, item.Status);
    }

    [Fact]
    public void UndoPurchased_RestoresApprovedWhenPriorApprovalTotalExists()
    {
        var item = NewItem();
        item.SetStatus(BudgetItemStatus.Approved);
        item.SetApprovedTotal(50m);
        item.MarkPurchased();
        item.UndoPurchased();
        Assert.Equal(BudgetItemStatus.Approved, item.Status);
        Assert.Equal(50m, item.ApprovedTotal);
    }

    [Fact]
    public void UndoPurchased_RestoresDraftWhenNoPriorApprovedTotalExists()
    {
        var item = NewItem();
        item.MarkPurchased();
        item.UndoPurchased();
        Assert.Equal(BudgetItemStatus.Draft, item.Status);
        Assert.Throws<InvalidOperationException>(() => item.UndoPurchased());
    }

    private static BudgetItem NewItem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "TEST-ITEM", "1", "Test purchase", 2m, 25m);
}
