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

    public BudgetVersion(
        Guid fiscalYearId,
        string name,
        BudgetVersionType versionType,
        int versionNumber,
        Guid? basedOnVersionId = null,
        DateOnly? effectiveDate = null,
        string? notes = null)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Version name is required.", nameof(name));
        if (name.Trim().Length > 100) throw new ArgumentException("Version name cannot exceed 100 characters.", nameof(name));
        if (!Enum.IsDefined(versionType)) throw new ArgumentOutOfRangeException(nameof(versionType));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (basedOnVersionId == Guid.Empty) throw new ArgumentException("Based-on version ID cannot be empty.", nameof(basedOnVersionId));

        FiscalYearId = fiscalYearId;
        Name = name.Trim();
        VersionType = versionType;
        VersionNumber = versionNumber;
        BasedOnVersionId = basedOnVersionId;
        EffectiveDate = effectiveDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

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

    public void UpdatePlanningMetadata(string name, DateOnly? effectiveDate, string? notes)
    {
        EnsureUnlocked();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Version name is required.", nameof(name));
        if (name.Trim().Length > 100) throw new ArgumentException("Version name cannot exceed 100 characters.", nameof(name));
        Name = name.Trim();
        EffectiveDate = effectiveDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Lock() => IsLocked = true;

    public void Approve(string actor, DateTimeOffset approvedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Approver is required.", nameof(actor));
        if (ApprovedAtUtc is not null) throw new InvalidOperationException("Budget version is already approved.");
        ApprovedBy = actor.Trim();
        ApprovedAtUtc = approvedAtUtc;
        IsLocked = true;
    }

    private void EnsureUnlocked()
    {
        if (IsLocked) throw new InvalidOperationException("Locked budget versions cannot be edited directly.");
    }
}
