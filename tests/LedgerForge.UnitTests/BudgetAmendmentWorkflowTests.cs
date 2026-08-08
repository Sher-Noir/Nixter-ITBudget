using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetAmendmentWorkflowTests
{
    [Fact]
    public void Amendment_FollowsSubmissionAndApprovalSequence()
    {
        var amendment = NewAmendment(250m);
        var now = DateTimeOffset.UtcNow;

        amendment.Submit("DOMAIN\\editor", now);
        Assert.Equal(BudgetAmendmentState.PendingApproval, amendment.State);

        amendment.Approve("DOMAIN\\approver", 1250m, "Approved for revised scope", now.AddMinutes(1));
        Assert.Equal(BudgetAmendmentState.Approved, amendment.State);
        Assert.Equal(1250m, amendment.ResultingRevisedTotal);
        Assert.Equal("DOMAIN\\approver", amendment.DecisionBy);
    }

    [Fact]
    public void Amendment_RejectsZeroDelta()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewAmendment(0m));
    }

    [Fact]
    public void Amendment_CannotApproveNegativeResult()
    {
        var amendment = NewAmendment(-500m);
        amendment.Submit("DOMAIN\\editor", DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            amendment.Approve("DOMAIN\\approver", -1m, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApprovedAmendment_CannotBeCancelled()
    {
        var amendment = NewAmendment(100m);
        var now = DateTimeOffset.UtcNow;
        amendment.Submit("DOMAIN\\editor", now);
        amendment.Approve("DOMAIN\\approver", 1100m, null, now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            amendment.Cancel("DOMAIN\\editor", "No longer required", now.AddMinutes(2)));
    }

    private static BudgetAmendment NewAmendment(decimal delta)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), delta, "Scope change");
}
