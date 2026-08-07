using LedgerForge.Application.Budgeting;
using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetAllocationReconcilerTests
{
    [Fact]
    public void Reconcile_PercentageAllocations_RequiresOneHundredPercent()
    {
        var itemId = Guid.NewGuid();
        var allocations = new[]
        {
            new BudgetItemAllocation(itemId, AllocationMethod.Percentage, 60m, null),
            new BudgetItemAllocation(itemId, AllocationMethod.Percentage, 30m, null)
        };

        var result = BudgetAllocationReconciler.Reconcile(1000m, allocations);

        Assert.False(result.IsValid);
        Assert.Equal(90m, result.PercentageTotal);
        Assert.Contains(result.Errors, error => error.Contains("100%", StringComparison.Ordinal));
    }

    [Fact]
    public void Reconcile_AmountAllocations_RequiresParentAmount()
    {
        var itemId = Guid.NewGuid();
        var allocations = new[]
        {
            new BudgetItemAllocation(itemId, AllocationMethod.Amount, null, 600m),
            new BudgetItemAllocation(itemId, AllocationMethod.Amount, null, 399.99m)
        };

        var result = BudgetAllocationReconciler.Reconcile(1000m, allocations);

        Assert.False(result.IsValid);
        Assert.Equal(999.99m, result.AmountTotal);
        Assert.Equal(0.01m, result.Difference);
    }

    [Fact]
    public void Reconcile_RejectsMixedAllocationMethods()
    {
        var itemId = Guid.NewGuid();
        var allocations = new[]
        {
            new BudgetItemAllocation(itemId, AllocationMethod.Percentage, 50m, null),
            new BudgetItemAllocation(itemId, AllocationMethod.Amount, null, 500m)
        };

        var result = BudgetAllocationReconciler.Reconcile(1000m, allocations);

        Assert.False(result.IsValid);
        Assert.Null(result.Method);
        Assert.Contains(result.Errors, error => error.Contains("cannot mix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reconcile_FullyReconciledAmounts_AreValid()
    {
        var itemId = Guid.NewGuid();
        var allocations = new[]
        {
            new BudgetItemAllocation(itemId, AllocationMethod.Amount, null, 600m),
            new BudgetItemAllocation(itemId, AllocationMethod.Amount, null, 400m)
        };

        var result = BudgetAllocationReconciler.Reconcile(1000m, allocations);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(0m, result.Difference);
    }
}
