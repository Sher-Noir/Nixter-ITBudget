using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class FiscalYearWorkflowTests
{
    [Fact]
    public void Close_ClearsCurrentAndPreventsMakingYearCurrentAgain()
    {
        var year = new FiscalYear("FY2030", new DateOnly(2029, 7, 1), new DateOnly(2030, 6, 30), 2030);
        year.SetStatus(FiscalYearStatus.Active);
        year.SetCurrent(true);

        year.Close(DateTimeOffset.UtcNow);

        Assert.Equal(FiscalYearStatus.Closed, year.Status);
        Assert.False(year.IsCurrent);
        Assert.Throws<InvalidOperationException>(() => year.SetCurrent(true));
    }

    [Fact]
    public void LockedFiscalYear_CannotChangeDetails()
    {
        var year = new FiscalYear("FY2030", new DateOnly(2029, 7, 1), new DateOnly(2030, 6, 30), 2030);
        year.Lock(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => year.UpdateDetails(
            "FY2030 Updated",
            year.StartDate,
            year.EndDate,
            2030,
            "Changed"));
    }

    [Fact]
    public void ApprovedBudgetVersion_IsLocked()
    {
        var version = new BudgetVersion(
            Guid.NewGuid(),
            "Initial Planning",
            BudgetVersionType.InitialPlanning,
            1);

        version.Approve("DOMAIN\\approver", DateTimeOffset.UtcNow);

        Assert.True(version.IsLocked);
        Assert.NotNull(version.ApprovedAtUtc);
        Assert.Throws<InvalidOperationException>(() => version.UpdatePlanningMetadata("Changed", null, null));
    }
}
