using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Actuals;

/// <summary>A monthly billing rule, scoped to one budget item and its fiscal year.</summary>
public sealed class RecurringActualSchedule : AuditableEntity
{
    private RecurringActualSchedule() { }

    public RecurringActualSchedule(
        Guid fiscalYearId, Guid budgetItemId, string description, decimal amount,
        bool lastDayOfMonth, int dayOfMonth, DateOnly startDate, DateOnly endDate,
        string? sourceReference = null, Guid? financeAccountId = null,
        Guid? departmentId = null, Guid? locationId = null)
    {
        if (fiscalYearId == Guid.Empty || budgetItemId == Guid.Empty)
            throw new ArgumentException("Fiscal year and budget item are required.");
        FiscalYearId = fiscalYearId;
        BudgetItemId = budgetItemId;
        Update(description, amount, lastDayOfMonth, dayOfMonth, startDate, endDate,
            sourceReference, financeAccountId, departmentId, locationId);
    }

    public Guid FiscalYearId { get; private set; }
    public Guid BudgetItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool LastDayOfMonth { get; private set; }
    public int DayOfMonth { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? SourceReference { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(
        string description, decimal amount, bool lastDayOfMonth, int dayOfMonth,
        DateOnly startDate, DateOnly endDate, string? sourceReference,
        Guid? financeAccountId, Guid? departmentId, Guid? locationId)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
            throw new ArgumentException("Description must contain 1-500 characters.", nameof(description));
        if (amount <= 0m || decimal.Round(amount, 4) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive with no more than four decimal places.");
        if (dayOfMonth is < 1 or > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Billing day must be between 1 and 31.");
        if (endDate < startDate)
            throw new ArgumentException("End date must not precede start date.", nameof(endDate));
        if (sourceReference?.Trim().Length > 200)
            throw new ArgumentException("Reference cannot exceed 200 characters.", nameof(sourceReference));
        if (financeAccountId == Guid.Empty || departmentId == Guid.Empty || locationId == Guid.Empty)
            throw new ArgumentException("Allocation identifiers cannot be empty GUIDs.");

        Description = description.Trim();
        Amount = amount;
        LastDayOfMonth = lastDayOfMonth;
        DayOfMonth = dayOfMonth;
        StartDate = startDate;
        EndDate = endDate;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim();
        FinanceAccountId = financeAccountId;
        DepartmentId = departmentId;
        LocationId = locationId;
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>Uses the actual last calendar day, including leap years.</summary>
    public DateOnly BillingDate(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, LastDayOfMonth ? days : Math.Min(DayOfMonth, days));
    }

    public bool Due(int year, int month, DateOnly localToday)
    {
        if (!IsActive) return false;
        var date = BillingDate(year, month);
        return date >= StartDate && date <= EndDate && date < localToday;
    }
}
