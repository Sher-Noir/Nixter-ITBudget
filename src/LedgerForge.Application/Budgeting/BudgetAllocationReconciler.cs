using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Application.Budgeting;

public sealed record BudgetAllocationReconciliation(
    bool IsValid,
    AllocationMethod? Method,
    decimal PercentageTotal,
    decimal AmountTotal,
    decimal ExpectedAmount,
    decimal Difference,
    IReadOnlyList<string> Errors);

public static class BudgetAllocationReconciler
{
    private const decimal PercentageTolerance = 0.0001m;
    private const decimal CurrencyTolerance = 0.0001m;

    public static BudgetAllocationReconciliation Reconcile(decimal expectedAmount, IReadOnlyCollection<BudgetItemAllocation> allocations)
    {
        if (expectedAmount < 0m) throw new ArgumentOutOfRangeException(nameof(expectedAmount));
        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Count == 0) return new(true, null, 0m, 0m, expectedAmount, expectedAmount, []);

        var errors = new List<string>();
        var methods = allocations.Select(x => x.Method).Distinct().ToArray();
        if (methods.Length != 1)
        {
            errors.Add("Allocation rows cannot mix percentage and amount methods for the same budget item.");
            return new(false, null, 0m, 0m, expectedAmount, expectedAmount, errors);
        }

        var method = methods[0];
        if (method == AllocationMethod.Percentage)
        {
            var percentageTotal = allocations.Sum(x => x.Percentage ?? 0m);
            var calculatedAmount = allocations.Sum(x => Math.Round(expectedAmount * (x.Percentage ?? 0m) / 100m, 4, MidpointRounding.AwayFromZero));
            var difference = expectedAmount - calculatedAmount;
            if (Math.Abs(100m - percentageTotal) > PercentageTolerance) errors.Add($"Allocation percentages total {percentageTotal:0.####}% instead of 100%.");
            if (Math.Abs(difference) > CurrencyTolerance) errors.Add($"Percentage allocation rounding leaves an unreconciled difference of {difference:0.0000}.");
            return new(errors.Count == 0, method, percentageTotal, calculatedAmount, expectedAmount, difference, errors);
        }

        var amountTotal = allocations.Sum(x => x.Amount ?? 0m);
        var amountDifference = expectedAmount - amountTotal;
        if (Math.Abs(amountDifference) > CurrencyTolerance) errors.Add($"Allocation amounts total {amountTotal:0.0000} instead of {expectedAmount:0.0000}.");
        return new(errors.Count == 0, method, 0m, amountTotal, expectedAmount, amountDifference, errors);
    }
}
