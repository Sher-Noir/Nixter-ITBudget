using Xunit;
using Crch.ItBudget.Application.Budgeting;

namespace Crch.ItBudget.UnitTests;

public sealed class BudgetCalculatorTests
{
    [Fact]
    public void PlannedTotal_IsQuantityTimesUnitCost()
    {
        Assert.Equal(1234.50m, BudgetCalculator.CalculatePlannedTotal(10m, 123.45m));
    }

    [Fact]
    public void PlannedTotal_RejectsNegativeQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BudgetCalculator.CalculatePlannedTotal(-1m, 10m));
    }
}
