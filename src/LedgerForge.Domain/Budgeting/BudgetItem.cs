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
        ValidateItemNumber(itemNumber);
        ValidateDescription(description);
        ValidateCost(quantity, unitCost);

        FiscalYearId = fiscalYearId;
        BudgetVersionId = budgetVersionId;
        StableIdentifier = stableIdentifier.Trim();
        ItemNumber = itemNumber.Trim();
        Description = description.Trim();
        Quantity = quantity;
        UnitCost = unitCost;
        PlannedTotal = DomainBudgetCalculator.Multiply(quantity, unitCost);
        Status = BudgetItemStatus.Draft;
        PurchaseType = PurchaseType.New;
    }

    public Guid FiscalYearId { get; private set; }
    public Guid BudgetVersionId { get; private set; }
    public string StableIdentifier { get; private set; } = string.Empty;
    public string ItemNumber { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ReasonPurpose { get; private set; }
    public PurchaseType PurchaseType { get; private set; }
    public Guid? BudgetSectionId { get; private set; }
    public Guid? FinanceTypeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? NeedLevelId { get; private set; }
    public Guid? InternalCategoryId { get; private set; }
    public Guid? FrequencyId { get; private set; }
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
        ValidateCost(quantity, unitCost);
        Quantity = quantity;
        UnitCost = unitCost;
        PlannedTotal = DomainBudgetCalculator.Multiply(quantity, unitCost);
    }

    public void UpdatePlanningDetails(
        string itemNumber,
        string description,
        string? reasonPurpose,
        PurchaseType purchaseType,
        DateOnly? estimatedPurchaseDate,
        DateOnly? renewalDate,
        Guid? budgetSectionId,
        Guid? financeTypeId,
        Guid? departmentId,
        Guid? locationId,
        Guid? needLevelId,
        Guid? internalCategoryId,
        Guid? frequencyId)
    {
        ValidateItemNumber(itemNumber);
        ValidateDescription(description);
        if (!Enum.IsDefined(purchaseType)) throw new ArgumentOutOfRangeException(nameof(purchaseType));

        ItemNumber = itemNumber.Trim();
        Description = description.Trim();
        ReasonPurpose = NormalizeOptional(reasonPurpose);
        PurchaseType = purchaseType;
        EstimatedPurchaseDate = estimatedPurchaseDate;
        RenewalDate = renewalDate;
        BudgetSectionId = NormalizeId(budgetSectionId, nameof(budgetSectionId));
        FinanceTypeId = NormalizeId(financeTypeId, nameof(financeTypeId));
        DepartmentId = NormalizeId(departmentId, nameof(departmentId));
        LocationId = NormalizeId(locationId, nameof(locationId));
        NeedLevelId = NormalizeId(needLevelId, nameof(needLevelId));
        InternalCategoryId = NormalizeId(internalCategoryId, nameof(internalCategoryId));
        FrequencyId = NormalizeId(frequencyId, nameof(frequencyId));
    }

    public void SetStatus(BudgetItemStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
    }

    public void SetApprovedTotal(decimal? approvedTotal)
    {
        if (approvedTotal < 0m) throw new ArgumentOutOfRangeException(nameof(approvedTotal));
        ApprovedTotal = approvedTotal;
    }

    public void SetRevisedTotal(decimal? revisedTotal)
    {
        if (revisedTotal < 0m) throw new ArgumentOutOfRangeException(nameof(revisedTotal));
        RevisedTotal = revisedTotal;
    }

    private static void ValidateItemNumber(string itemNumber)
    {
        if (string.IsNullOrWhiteSpace(itemNumber)) throw new ArgumentException("Item number is required.", nameof(itemNumber));
        if (itemNumber.Trim().Length > 64) throw new ArgumentException("Item number cannot exceed 64 characters.", nameof(itemNumber));
    }

    private static void ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (description.Trim().Length > 500) throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));
    }

    private static void ValidateCost(decimal quantity, decimal unitCost)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Lookup identifiers cannot be empty GUIDs.", parameterName);
        return value;
    }
}

internal static class DomainBudgetCalculator
{
    public static decimal Multiply(decimal quantity, decimal unitCost) => checked(quantity * unitCost);
}
