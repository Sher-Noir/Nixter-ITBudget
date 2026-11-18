using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>
/// Estimates the expected one-time spend by today. Neither an undated
/// item nor a past estimated purchase date is proof a purchase happened.
/// Only a recorded purchase or posted actual enables its planned allowance.
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

        // An estimated purchase date is a planning intention, not evidence
        // that an expense was incurred. Count an expected one-time charge
        // only when it is marked Purchased or a net-positive actual exists.
        if (status == BudgetItemStatus.Purchased || postedActual > 0m)
            return currentBudget;

        // No reliable billing or purchase date => no expected-to-date charge.
        // A guessed calendar proration produced false under-budget balances
        // for unscheduled equipment and annual renewals.
        return 0m;
    }
}
