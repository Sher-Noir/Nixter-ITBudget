using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetRevisionWorkflowTests
{
    private static BudgetItem NewItem()
        => new(Guid.NewGuid(), Guid.NewGuid(), "LF-AUTO-TEST", "1", "New hardware", 2m, 125m);

    [Fact]
    public void SubmittedItem_CanReopenForEditingAndResubmit()
    {
        var item = NewItem();
        item.Submit("TEST\\submitter", DateTimeOffset.UtcNow);

        item.ReopenForRevision();
        Assert.Equal(BudgetItemStatus.Draft, item.Status);
        Assert.True(item.IsPlanningEditable);
        item.ChangeCost(3m, 125m);
        item.UpdateVendorAndNotes(Guid.NewGuid(), "Adjusted after submission");
        Assert.Equal(375m, item.PlannedTotal);

        item.Submit("TEST\\editor", DateTimeOffset.UtcNow);
        Assert.Equal(BudgetItemStatus.Submitted, item.Status);
    }

    [Fact]
    public void ApprovedItem_ReopensWithOriginalApprovedAmountPreserved()
    {
        var item = NewItem();
        item.Submit("TEST\\submitter", DateTimeOffset.UtcNow);
        item.Approve("TEST\\approver", DateTimeOffset.UtcNow);
        Assert.Equal(250m, item.ApprovedTotal);

        item.ReopenForRevision();
        item.ChangeCost(4m, 125m);

        Assert.Equal(BudgetItemStatus.Draft, item.Status);
        Assert.Equal(250m, item.ApprovedTotal);
        Assert.Equal(500m, item.PlannedTotal);

        item.Submit("TEST\\editor", DateTimeOffset.UtcNow);
        item.Approve("TEST\\approver", DateTimeOffset.UtcNow);
        Assert.Equal(500m, item.ApprovedTotal);
    }

    [Fact]
    public void DraftItem_CannotReopen_WithoutPriorSubmission()
    {
        var item = NewItem();
        Assert.Throws<InvalidOperationException>(item.ReopenForRevision);
    }
}
