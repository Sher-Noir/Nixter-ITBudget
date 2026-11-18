namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>Over/Under is the difference between expected and actual for eligible items only.</summary>
public sealed record BudgetOverUnderContribution(
    decimal ExpectedIncluded, decimal ActualIncluded,
    decimal ExpectedExcluded, decimal ActualExcluded, decimal Variance);

public static class BudgetOverUnderCalculator
{
    public static BudgetOverUnderContribution Calculate(
        decimal expectedToDate, decimal actual, bool excluded)
    {
        if (excluded)
            return new(0m, 0m, expectedToDate, actual, 0m);
        return new(expectedToDate, actual, 0m, 0m, expectedToDate - actual);
    }
}
