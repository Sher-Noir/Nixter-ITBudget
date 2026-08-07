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
        ValidateDetails(displayName, startDate, endDate, planningYear);
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

    public void UpdateDetails(
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description)
    {
        EnsureEditable();
        ValidateDetails(displayName, startDate, endDate, planningYear);
        DisplayName = displayName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        PlanningYear = planningYear;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetStatus(FiscalYearStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Closed or archived fiscal years cannot change workflow status directly.");
        Status = status;
    }

    public void SetCurrent(bool isCurrent)
    {
        if (isCurrent && Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Closed or archived fiscal years cannot be current.");
        IsCurrent = isCurrent;
    }

    public void Lock(DateTimeOffset lockedAtUtc)
    {
        if (Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Closed or archived fiscal years cannot be locked again.");
        LockedAtUtc ??= lockedAtUtc;
    }

    public void Close(DateTimeOffset closedAtUtc)
    {
        if (Status == FiscalYearStatus.Archived)
            throw new InvalidOperationException("Archived fiscal years cannot be closed again.");
        Status = FiscalYearStatus.Closed;
        IsCurrent = false;
        LockedAtUtc ??= closedAtUtc;
        ClosedAtUtc = closedAtUtc;
    }

    private void EnsureEditable()
    {
        if (LockedAtUtc is not null || Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Locked, closed, or archived fiscal years cannot be edited directly.");
    }

    private static void ValidateDetails(string displayName, DateOnly startDate, DateOnly endDate, int planningYear)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        if (displayName.Trim().Length > 32) throw new ArgumentException("Display name cannot exceed 32 characters.", nameof(displayName));
        if (endDate < startDate) throw new ArgumentOutOfRangeException(nameof(endDate), "End date must not precede start date.");
        if (planningYear < 1900 || planningYear > 9999) throw new ArgumentOutOfRangeException(nameof(planningYear));
    }
}
