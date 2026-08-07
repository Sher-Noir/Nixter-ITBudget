using LedgerForge.Domain.Procurement;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class PurchaseOrderWorkflowTests
{
    [Fact]
    public void PurchaseOrder_FollowsApprovalAndIssueSequence()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "PO-1001", "Infrastructure renewal");
        var now = DateTimeOffset.UtcNow;

        order.Submit("DOMAIN\\editor", now);
        Assert.Equal(PurchaseOrderState.PendingApproval, order.State);

        order.Approve("DOMAIN\\approver", now.AddMinutes(1));
        Assert.Equal(PurchaseOrderState.Approved, order.State);

        order.Issue("DOMAIN\\editor", now.AddMinutes(2));
        Assert.Equal(PurchaseOrderState.Issued, order.State);

        order.Close("DOMAIN\\editor", now.AddMinutes(3));
        Assert.Equal(PurchaseOrderState.Closed, order.State);
    }

    [Fact]
    public void PurchaseOrder_CannotIssueBeforeApproval()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "PO-1002", "Unsupported transition");

        Assert.Throws<InvalidOperationException>(() => order.Issue("DOMAIN\\editor", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PurchaseOrderLine_RecalculatesAuthoritativeTotal()
    {
        var line = new PurchaseOrderLine(Guid.NewGuid(), 1, "Licenses", 12m, 25.50m);

        Assert.Equal(306m, line.LineTotal);

        line.Update("Licenses", 10m, 30m, null, null, null, null);
        Assert.Equal(300m, line.LineTotal);
    }

    [Fact]
    public void CancelledPurchaseOrder_CannotBeSubmitted()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "PO-1003", "Cancelled order");
        order.Cancel("DOMAIN\\editor", "No longer needed", DateTimeOffset.UtcNow);

        Assert.Equal(PurchaseOrderState.Cancelled, order.State);
        Assert.Throws<InvalidOperationException>(() => order.Submit("DOMAIN\\editor", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PendingPurchaseOrder_CanBeRejectedWithReason()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "PO-1004", "Rejected order");
        var now = DateTimeOffset.UtcNow;
        order.Submit("DOMAIN\\editor", now);

        order.Reject("DOMAIN\\approver", "Funding source changed.", now.AddMinutes(1));

        Assert.Equal(PurchaseOrderState.Rejected, order.State);
        Assert.Equal("DOMAIN\\approver", order.RejectedBy);
        Assert.Equal("Funding source changed.", order.RejectionReason);
        Assert.Throws<InvalidOperationException>(() => order.Approve("DOMAIN\\approver", now.AddMinutes(2)));
    }
}
