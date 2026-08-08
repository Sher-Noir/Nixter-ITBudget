using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetItemApprovalWorkflowTests
{
    [Fact]
    public void SubmittedItem_BecomesReadOnlyAndCanBeApproved()
    {
        var item = NewItem();
        var now = DateTimeOffset.UtcNow;

        item.Submit("DOMAIN\\editor", now);

        Assert.Equal(BudgetItemStatus.Submitted, item.Status);
        Assert.False(item.IsPlanningEditable);
        Assert.Throws<InvalidOperationException>(() => item.ChangeCost(2m, 50m));

        item.Approve("DOMAIN\\approver", now.AddMinutes(1), "Approved for current plan.");

        Assert.Equal(BudgetItemStatus.Approved, item.Status);
        Assert.Equal(item.PlannedTotal, item.ApprovedTotal);
        Assert.Equal("DOMAIN\\approver", item.DecisionBy);
        Assert.NotNull(item.DecisionAtUtc);
    }

    [Fact]
    public void Denial_RequiresReason()
    {
        var item = NewItem();
        item.Submit("DOMAIN\\editor", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            item.Deny("DOMAIN\\approver", "", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DeferredItem_ReturnsToPlanningEditableState()
    {
        var item = NewItem();
        item.Submit("DOMAIN\\editor", DateTimeOffset.UtcNow);
        item.Defer("DOMAIN\\approver", "Need an updated quote.", DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(BudgetItemStatus.Deferred, item.Status);
        Assert.True(item.IsPlanningEditable);

        item.ChangeCost(2m, 75m);
        Assert.Equal(150m, item.PlannedTotal);
    }

    private static BudgetItem NewItem()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LF-test",
            "100",
            "Test item",
            1m,
            100m);
}
