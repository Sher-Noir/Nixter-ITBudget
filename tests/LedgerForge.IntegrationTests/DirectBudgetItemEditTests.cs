using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class DirectBudgetItemEditTests
{
    [Theory]
    [InlineData(BudgetItemStatus.Approved)]
    [InlineData(BudgetItemStatus.Active)]
    [InlineData(BudgetItemStatus.Purchased)]
    public void DirectEdit_KeepsOperationalStatusAndOriginalApproval(BudgetItemStatus status)
    {
        var item = NewItem();
        item.SetApprovedTotal(120m);
        item.SetStatus(status);

        item.ChangeCost(3m, 50m, allowDirectApprovedEdits: true);
        item.UpdatePlanningDetails(
            "1", "Updated description", "Updated notes", PurchaseType.Renewal,
            null, null, null, null, null, null, null, null, null,
            allowDirectApprovedEdits: true);
        item.UpdateVendorAndNotes(null, "Updated vendor notes", allowDirectApprovedEdits: true);

        Assert.Equal(status, item.Status);
        Assert.Equal(150m, item.PlannedTotal);
        Assert.Equal(150m, item.RevisedTotal);
        Assert.Equal(120m, item.ApprovedTotal);
        Assert.Equal("Updated description", item.Description);
        Assert.Equal("Updated vendor notes", item.Notes);
        Assert.False(item.IsPlanningEditable);
    }

    [Fact]
    public void DirectEdit_ChangingOnlyMetadata_DoesNotOverwriteExistingRevisedTotal()
    {
        var item = NewItem();
        item.SetApprovedTotal(100m);
        item.SetRevisedTotal(140m);
        item.SetStatus(BudgetItemStatus.Approved);

        item.ChangeCost(2m, 50m, allowDirectApprovedEdits: true);
        item.UpdateVendorAndNotes(null, "Just updated notes", allowDirectApprovedEdits: true);

        Assert.Equal(140m, item.RevisedTotal);
        Assert.Equal(100m, item.ApprovedTotal);
    }

    [Theory]
    [InlineData(BudgetItemStatus.Approved)]
    [InlineData(BudgetItemStatus.Purchased)]
    public void StrictMode_StillRequiresReopeningBeforePlanningEdit(BudgetItemStatus status)
    {
        var item = NewItem();
        item.SetStatus(status);

        Assert.Throws<InvalidOperationException>(() => item.ChangeCost(3m, 50m));
        Assert.Throws<InvalidOperationException>(() => item.UpdateVendorAndNotes(null, "Not allowed"));
        Assert.Equal(100m, item.PlannedTotal);
    }

    [Theory]
    [InlineData(BudgetItemStatus.Submitted)]
    [InlineData(BudgetItemStatus.Denied)]
    [InlineData(BudgetItemStatus.Cancelled)]
    [InlineData(BudgetItemStatus.Archived)]
    public void EvenWithDirectEditEnabled_ProtectedStatesRemainImmutable(BudgetItemStatus status)
    {
        var item = NewItem();
        item.SetStatus(status);
        Assert.Throws<InvalidOperationException>(() =>
            item.ChangeCost(3m, 50m, allowDirectApprovedEdits: true));
    }

    [Fact]
    public void DirectEdit_ChangedCost_AffectsRevisedTotalButDoesNotChangeApprovedTotal()
    {
        var item = NewItem();
        item.SetStatus(BudgetItemStatus.Approved);
        item.SetApprovedTotal(110m);
        item.ChangeCost(5m, 25m, allowDirectApprovedEdits: true);

        Assert.Equal(125m, item.PlannedTotal);
        Assert.Equal(125m, item.RevisedTotal);
        Assert.Equal(110m, item.ApprovedTotal);
        Assert.Equal(BudgetItemStatus.Approved, item.Status);
    }

    private static BudgetItem NewItem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "TEST-ITEM", "1", "Original description", 2m, 50m);
}
