using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>
/// Estimates the expected one-time spend by today. An undated item is not
/// invented fiscal-year proration: posted actual spend or purchased status
/// makes the full budget due; otherwise no expected one-time spend is assumed.
/// </summary>
public static class BudgetSpendPacingCalculator
{
    public static decimal ExpectedOneTimeToDate(
        decimal currentBudget,
        DateOnly? estimatedPurchaseDate,
        BudgetItemStatus status,
        decimal postedActual,
        DateOnly fiscalYearStart,
        DateOnly fiscalYearEnd,
        DateOnly today)
    {
        if (currentBudget < 0m)
            throw new ArgumentOutOfRangeException(nameof(currentBudget));
        if (fiscalYearEnd < fiscalYearStart)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearEnd));

        if (status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived)
            return 0m;

        if (estimatedPurchaseDate is DateOnly date)
            return date <= today ? currentBudget : 0m;

        if (status == BudgetItemStatus.Purchased || postedActual > 0m)
            return currentBudget;

        // No reliable billing or purchase date => no expected-to-date charge.
        // A guessed calendar proration produced false under-budget balances
        // for unscheduled equipment and annual renewals.
        return 0m;
    }
}
