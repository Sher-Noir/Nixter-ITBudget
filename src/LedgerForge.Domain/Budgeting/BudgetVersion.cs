using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Budgeting;

public enum BudgetVersionType
{
    InitialPlanning,
    SubmittedBudget,
    ApprovedBaseline,
    RevisedBudget,
    Forecast,
    YearEndFinal
}

public sealed class BudgetVersion : AuditableEntity
{
    private BudgetVersion() { }

    public Guid FiscalYearId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public BudgetVersionType VersionType { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? BasedOnVersionId { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public bool IsLocked { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? Notes { get; private set; }
}
