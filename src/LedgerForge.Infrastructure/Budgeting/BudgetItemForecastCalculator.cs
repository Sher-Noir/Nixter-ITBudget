namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>
/// Item-level expected full-year spend. A published scenario is a total, not
/// an incremental obligation. PO commitments cover recurring expenses first
/// when an explicit PO-to-schedule relationship cannot be established.
/// </summary>
public sealed record BudgetItemForecastBreakdown(
    decimal PostedActual,
    decimal UnpostedRecurring,
    decimal OutstandingCommitments,
    decimal AdditionalUncoveredCommitments,
    decimal OtherExpectedFromPublishedScenario,
    decimal? PublishedScenarioTotal,
    decimal Total);

public static class BudgetItemForecastCalculator
{
    public static BudgetItemForecastBreakdown Calculate(
        decimal actual, decimal outstandingCommitments, decimal unpostedRecurring,
        decimal? publishedScenarioTotal)
    {
        if (outstandingCommitments < 0m || unpostedRecurring < 0m)
            throw new ArgumentOutOfRangeException(nameof(outstandingCommitments),
                "Commitments and unposted recurring charges cannot be negative.");

        // Avoid double-counting PO obligations potentially covering recurring bills.
        // This conservative assumption may omit independent commitments: users
        // can represent additional expected spend via published forecast scenarios.
        var extraCommitments = Math.Max(0m, outstandingCommitments - unpostedRecurring);
        var supportedTotal = actual + unpostedRecurring + extraCommitments;
        var extraForecast = Math.Max(0m, (publishedScenarioTotal ?? 0m) - supportedTotal);
        return new(actual, unpostedRecurring, outstandingCommitments,
            extraCommitments, extraForecast, publishedScenarioTotal,
            supportedTotal + extraForecast);
    }
}
