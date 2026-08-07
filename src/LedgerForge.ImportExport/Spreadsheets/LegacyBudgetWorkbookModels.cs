namespace LedgerForge.ImportExport.Spreadsheets;

public sealed record LegacyBudgetRow(
    int SourceRowNumber,
    int ItemNumber,
    string ItemDescription,
    string ReasonPurpose,
    DateOnly? EstimatedMonth,
    string FinanceType,
    string Department,
    string Location,
    decimal Quantity,
    decimal UnitCost,
    decimal SourcePlannedTotal,
    string NeedLevel,
    string? Notes,
    string InternalCategory,
    string Frequency,
    DateOnly? RenewalDate,
    string? Vendor,
    string Status,
    bool Approval,
    bool Denial,
    bool Proposed)
{
    public decimal RecalculatedPlannedTotal => checked(Quantity * UnitCost);
}

public sealed record ImportReconciliationExpectation(
    int? BudgetItemCount = null,
    decimal? PlannedTotal = null,
    string? PriorityNeedLevel = null,
    int? PriorityNeedLevelCount = null);

public sealed record LegacyBudgetReconciliation(
    int BudgetItemCount,
    decimal RecalculatedPlannedTotal,
    int PlannedTotalMismatchCount,
    int PriorityNeedLevelCount,
    ImportReconciliationExpectation? Expectation)
{
    public bool MatchesExpectation =>
        PlannedTotalMismatchCount == 0 &&
        (Expectation?.BudgetItemCount is null || BudgetItemCount == Expectation.BudgetItemCount) &&
        (Expectation?.PlannedTotal is null || RecalculatedPlannedTotal == Expectation.PlannedTotal) &&
        (Expectation?.PriorityNeedLevelCount is null || PriorityNeedLevelCount == Expectation.PriorityNeedLevelCount);
}

public sealed record LegacyBudgetWorkbookReadResult(
    string SourceFileName,
    string SourceSha256,
    IReadOnlyList<LegacyBudgetRow> BudgetRows,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LookupValues,
    LegacyBudgetReconciliation Reconciliation,
    IReadOnlyList<string> Warnings);

public sealed class LegacyBudgetWorkbookValidationException : InvalidDataException
{
    public LegacyBudgetWorkbookValidationException(string message) : base(message) { }
}
