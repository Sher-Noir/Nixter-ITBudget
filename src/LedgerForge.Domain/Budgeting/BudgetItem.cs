using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Budgeting;

public enum PurchaseType { New, Replacement, Renewal, Expansion, Project, Other }
public enum BudgetItemStatus { Draft, Proposed, Submitted, Approved, Denied, Deferred, Active, Cancelled, Archived }

public sealed class BudgetItem : AuditableEntity
{
    private BudgetItem() { }

    public BudgetItem(Guid fiscalYearId, Guid budgetVersionId, string stableIdentifier, string itemNumber, string description, decimal quantity, decimal unitCost)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (budgetVersionId == Guid.Empty) throw new ArgumentException("Budget version is required.", nameof(budgetVersionId));
        if (string.IsNullOrWhiteSpace(stableIdentifier)) throw new ArgumentException("Stable identifier is required.", nameof(stableIdentifier));
        if (string.IsNullOrWhiteSpace(itemNumber)) throw new ArgumentException("Item number is required.", nameof(itemNumber));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));

        FiscalYearId = fiscalYearId;
        BudgetVersionId = budgetVersionId;
        StableIdentifier = stableIdentifier.Trim();
        ItemNumber = itemNumber.Trim();
        Description = description.Trim();
        Quantity = quantity;
        UnitCost = unitCost;
        PlannedTotal = DomainBudgetCalculator.Multiply(quantity, unitCost);
        Status = BudgetItemStatus.Draft;
    }

    public Guid FiscalYearId { get; private set; }
    public Guid BudgetVersionId { get; private set; }
    public string StableIdentifier { get; private set; } = string.Empty;
    public string ItemNumber { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ReasonPurpose { get; private set; }
    public PurchaseType PurchaseType { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal PlannedTotal { get; private set; }
    public decimal? ApprovedTotal { get; private set; }
    public decimal? RevisedTotal { get; private set; }
    public DateOnly? EstimatedPurchaseDate { get; private set; }
    public DateOnly? RenewalDate { get; private set; }
    public BudgetItemStatus Status { get; private set; }

    public void ChangeCost(decimal quantity, decimal unitCost)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        Quantity = quantity;
        UnitCost = unitCost;
        PlannedTotal = DomainBudgetCalculator.Multiply(quantity, unitCost);
    }
}

internal static class DomainBudgetCalculator
{
    public static decimal Multiply(decimal quantity, decimal unitCost) => checked(quantity * unitCost);
}
