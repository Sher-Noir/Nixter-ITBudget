namespace LedgerForge.Application.Budgeting;

public static class BudgetCalculator
{
    public static decimal CalculatePlannedTotal(decimal quantity, decimal unitCost)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        return checked(quantity * unitCost);
    }

    public static decimal CalculateAvailable(decimal revisedBudget, decimal commitments, decimal actuals)
        => revisedBudget - commitments - actuals;
}
