using Crch.ItBudget.Domain.Common;

namespace Crch.ItBudget.Domain.Budgeting;

public enum AllocationMethod
{
    Percentage,
    Amount
}

public sealed class BudgetItemAllocation : AuditableEntity
{
    private BudgetItemAllocation() { }

    public BudgetItemAllocation(
        Guid budgetItemId,
        AllocationMethod method,
        decimal? percentage,
        decimal? amount,
        Guid? departmentId = null,
        Guid? locationId = null,
        Guid? financeAccountId = null,
        Guid? fiscalPeriodId = null,
        string? notes = null)
    {
        if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item is required.", nameof(budgetItemId));
        Validate(method, percentage, amount);

        BudgetItemId = budgetItemId;
        Method = method;
        Percentage = percentage;
        Amount = amount;
        DepartmentId = departmentId;
        LocationId = locationId;
        FinanceAccountId = financeAccountId;
        FiscalPeriodId = fiscalPeriodId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid BudgetItemId { get; private set; }
    public AllocationMethod Method { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public Guid? FiscalPeriodId { get; private set; }
    public decimal? Percentage { get; private set; }
    public decimal? Amount { get; private set; }
    public string? Notes { get; private set; }

    public void ChangeValue(AllocationMethod method, decimal? percentage, decimal? amount)
    {
        Validate(method, percentage, amount);
        Method = method;
        Percentage = percentage;
        Amount = amount;
    }

    private static void Validate(AllocationMethod method, decimal? percentage, decimal? amount)
    {
        switch (method)
        {
            case AllocationMethod.Percentage:
                if (percentage is null || percentage < 0m || percentage > 100m)
                {
                    throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage allocations must be between 0 and 100.");
                }
                if (amount is not null) throw new ArgumentException("Amount must be null for percentage allocations.", nameof(amount));
                break;

            case AllocationMethod.Amount:
                if (amount is null || amount < 0m)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount), "Amount allocations must be zero or greater.");
                }
                if (percentage is not null) throw new ArgumentException("Percentage must be null for amount allocations.", nameof(percentage));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }
}
