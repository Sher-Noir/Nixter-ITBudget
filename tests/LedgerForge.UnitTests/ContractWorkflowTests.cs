using LedgerForge.Domain.Procurement;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ContractWorkflowTests
{
    [Fact]
    public void Contract_ActivatesAndCalculatesNoticeDate()
    {
        var contract = NewContract(autoRenew: true, renewalNoticeDays: 60);
        var now = DateTimeOffset.UtcNow;

        contract.Activate("DOMAIN\\procurement", now);

        Assert.Equal(ContractState.Active, contract.State);
        Assert.Equal(new DateOnly(2027, 11, 1), contract.RenewalNoticeDate);
        Assert.Equal("DOMAIN\\procurement", contract.ActivatedBy);
    }

    [Fact]
    public void ActiveContract_CanBeTerminatedWithReason()
    {
        var contract = NewContract();
        var now = DateTimeOffset.UtcNow;
        contract.Activate("DOMAIN\\procurement", now);

        contract.Terminate("DOMAIN\\manager", "Service consolidation", now.AddDays(1));

        Assert.Equal(ContractState.Terminated, contract.State);
        Assert.Equal("Service consolidation", contract.TerminationReason);
    }

    [Fact]
    public void RenewalDecision_RequiresConcreteDecisionAndNote()
    {
        var renewal = new ContractRenewal(
            Guid.NewGuid(),
            new DateOnly(2027, 12, 31),
            new DateOnly(2027, 11, 1),
            12000m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            renewal.Decide(ContractRenewalStatus.PendingReview, "DOMAIN\\approver", "Review", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() =>
            renewal.Decide(ContractRenewalStatus.Renew, "DOMAIN\\approver", "", DateTimeOffset.UtcNow));

        renewal.Decide(ContractRenewalStatus.Renegotiate, "DOMAIN\\approver", "Seek revised pricing", DateTimeOffset.UtcNow);
        Assert.Equal(ContractRenewalStatus.Renegotiate, renewal.Status);
    }

    [Fact]
    public void RenewalCompletion_RequiresConcreteDecision()
    {
        var renewal = new ContractRenewal(
            Guid.NewGuid(),
            new DateOnly(2027, 12, 31),
            new DateOnly(2027, 11, 1),
            12000m);

        Assert.Throws<InvalidOperationException>(() =>
            renewal.Complete("DOMAIN\\procurement", "Completed", DateTimeOffset.UtcNow));

        renewal.Decide(ContractRenewalStatus.Renew, "DOMAIN\\approver", "Approved renewal", DateTimeOffset.UtcNow);
        renewal.Complete("DOMAIN\\procurement", "Renewal order executed", DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(ContractRenewalStatus.Completed, renewal.Status);
    }

    private static Contract NewContract(bool autoRenew = false, int renewalNoticeDays = 60)
        => new(
            Guid.NewGuid(),
            "CTR-1001",
            "Core platform subscription",
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 12, 31),
            12000m,
            autoRenew,
            renewalNoticeDays);
}
