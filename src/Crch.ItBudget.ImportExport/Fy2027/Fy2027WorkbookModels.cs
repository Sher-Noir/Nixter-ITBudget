namespace Crch.ItBudget.ImportExport.Fy2027;

public sealed record Fy2027BudgetRow(
    int SourceRowNumber,
    int ItemNumber,
    string ItemDescription,
    string ReasonPurpose,
    string? EstimatedMonth,
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

    public bool IsMustHave => string.Equals(
        NeedLevel,
        "4 = Must Have",
        StringComparison.OrdinalIgnoreCase);
}

public sealed record Fy2027Reconciliation(
    int BudgetItemCount,
    decimal RecalculatedPlannedTotal,
    int MustHaveCount,
    int PlannedTotalMismatchCount)
{
    public bool MeetsTargets =>
        BudgetItemCount == MigrationReconciliationTargets.Fy2027BudgetItemCount &&
        RecalculatedPlannedTotal == MigrationReconciliationTargets.Fy2027PlannedTotal &&
        MustHaveCount == MigrationReconciliationTargets.Fy2027MustHaveCount &&
        PlannedTotalMismatchCount == 0;
}

public sealed record Fy2027WorkbookReadResult(
    string SourceFileName,
    string SourceSha256,
    IReadOnlyList<Fy2027BudgetRow> BudgetRows,
    IReadOnlyDictionary<string, IReadOnlyList<string>> LookupValues,
    Fy2027Reconciliation Reconciliation,
    IReadOnlyList<string> Warnings);

public sealed class Fy2027WorkbookValidationException : InvalidDataException
{
    public Fy2027WorkbookValidationException(string message)
        : base(message)
    {
    }
}
