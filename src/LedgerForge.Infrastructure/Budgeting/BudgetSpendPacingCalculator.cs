using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>
/// Estimates the expected one-time spend by today. An undated item is not
/// silently valued at zero: prior actual spend or purchased status means its
/// full budget is due, otherwise its amount is spread evenly over the fiscal year.
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

        var fiscalDays = fiscalYearEnd.DayNumber - fiscalYearStart.DayNumber + 1;
        var elapsedDays = Math.Clamp(today.DayNumber - fiscalYearStart.DayNumber + 1,
            0, fiscalDays);
        return decimal.Round(currentBudget * elapsedDays / fiscalDays, 2,
            MidpointRounding.AwayFromZero);
    }
}
