using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Budgeting;

public enum FiscalYearStatus
{
    Draft,
    Planning,
    DepartmentReview,
    ApprovalPending,
    Approved,
    Active,
    Closed,
    Archived
}

public sealed class FiscalYear : AuditableEntity
{
    private FiscalYear() { }

    public FiscalYear(string displayName, DateOnly startDate, DateOnly endDate, int planningYear)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        if (endDate < startDate) throw new ArgumentOutOfRangeException(nameof(endDate), "End date must not precede start date.");
        DisplayName = displayName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        PlanningYear = planningYear;
        Status = FiscalYearStatus.Draft;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public int PlanningYear { get; private set; }
    public FiscalYearStatus Status { get; private set; }
    public bool IsCurrent { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
}
