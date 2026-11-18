using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetBillingPace(
    BudgetBillingCadence EffectiveCadence,
    string Basis,
    int BilledPeriods,
    decimal BudgetPerPeriod,
    decimal ExpectedToDate,
    decimal RemainingFullYearAllowance);

/// <summary>
/// Actual-month paced comparisons. A month is counted once if its net booked
/// amount is positive; corrections and split invoices do not create extra periods.
/// </summary>
public static class BudgetBillingPaceCalculator
{
    public static BudgetBillingCadence Resolve(
        BudgetBillingCadence configured, IReadOnlyCollection<int> billedMonthIndexes)
    {
        if (configured != BudgetBillingCadence.Automatic) return configured;
        var sorted = billedMonthIndexes.Distinct().OrderBy(x => x).ToArray();
        var consecutive = 1;
        for (var i = 1; i < sorted.Length; i++)
        {
            consecutive = sorted[i] == sorted[i - 1] + 1 ? consecutive + 1 : 1;
            if (consecutive >= 3) return BudgetBillingCadence.Monthly;
        }
        return BudgetBillingCadence.OneTime;
    }

    public static BudgetBillingPace Calculate(
        decimal annualBudget, BudgetBillingCadence configured,
        IReadOnlyCollection<int> billedMonths,
        IReadOnlyCollection<int> billedQuarters)
    {
        if (annualBudget < 0m) throw new ArgumentOutOfRangeException(nameof(annualBudget));
        var effective = Resolve(configured, billedMonths);
        var automatic = configured == BudgetBillingCadence.Automatic;
        if (effective is not (BudgetBillingCadence.Monthly or BudgetBillingCadence.Quarterly))
            return new(effective, automatic ? "Automatic: no confirmed monthly pattern" : "One-time",
                0, 0m, 0m, 0m);

        var periods = effective == BudgetBillingCadence.Monthly ? 12 : 4;
        var count = Math.Min(periods,
            effective == BudgetBillingCadence.Monthly
                ? billedMonths.Distinct().Count()
                : billedQuarters.Distinct().Count());
        var perPeriod = decimal.Round(annualBudget / periods, 2, MidpointRounding.AwayFromZero);
        var expected = decimal.Round(annualBudget * count / periods, 2,
            MidpointRounding.AwayFromZero);
        return new(
            effective,
            automatic ? "Detected monthly bills in 3+ consecutive months" :
                effective == BudgetBillingCadence.Monthly ? "Monthly billing (manual selection)" :
                "Quarterly billing (manual selection)",
            count, perPeriod, expected, Math.Max(0m, annualBudget - expected));
    }
}
