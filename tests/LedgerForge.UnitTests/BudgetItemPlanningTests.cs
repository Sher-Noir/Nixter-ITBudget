using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetItemPlanningTests
{
    [Fact]
    public void UpdatePlanningDetails_PreservesStableIdentifierAndSetsDimensions()
    {
        var fiscalYearId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var item = new BudgetItem(
            fiscalYearId,
            versionId,
            "LF-STABLE-001",
            "100",
            "Laptop replacement",
            2m,
            1200m);

        item.UpdatePlanningDetails(
            "100A",
            "Laptop replacement program",
            "Replace aging devices.",
            PurchaseType.Replacement,
            new DateOnly(2030, 8, 1),
            null,
            null,
            null,
            departmentId,
            locationId,
            null,
            null,
            null);

        Assert.Equal("LF-STABLE-001", item.StableIdentifier);
        Assert.Equal("100A", item.ItemNumber);
        Assert.Equal(PurchaseType.Replacement, item.PurchaseType);
        Assert.Equal(departmentId, item.DepartmentId);
        Assert.Equal(locationId, item.LocationId);
    }

    [Fact]
    public void ChangeCost_RecalculatesPlannedTotal()
    {
        var item = new BudgetItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LF-STABLE-002",
            "200",
            "Software licenses",
            10m,
            25m);

        item.ChangeCost(12m, 30m);

        Assert.Equal(360m, item.PlannedTotal);
    }

    [Fact]
    public void UpdatePlanningDetails_RejectsEmptyLookupGuid()
    {
        var item = new BudgetItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LF-STABLE-003",
            "300",
            "Network service",
            1m,
            100m);

        Assert.Throws<ArgumentException>(() => item.UpdatePlanningDetails(
            item.ItemNumber,
            item.Description,
            null,
            PurchaseType.Renewal,
            null,
            null,
            null,
            null,
            Guid.Empty,
            null,
            null,
            null,
            null));
    }
}
