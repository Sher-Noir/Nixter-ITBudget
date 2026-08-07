using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Procurement;

public enum PurchaseOrderState
{
    Draft,
    PendingApproval,
    Approved,
    Issued,
    Closed,
    Cancelled,
    Rejected
}

public sealed class PurchaseOrder : AuditableEntity
{
    private PurchaseOrder() { }

    public PurchaseOrder(Guid fiscalYearId, Guid vendorId, string number, string description)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor is required.", nameof(vendorId));
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Purchase order number is required.", nameof(number));
        if (number.Trim().Length > 100) throw new ArgumentException("Purchase order number cannot exceed 100 characters.", nameof(number));
        FiscalYearId = fiscalYearId;
        VendorId = vendorId;
        Number = number.Trim();
        State = PurchaseOrderState.Draft;
        UpdateDescription(description);
    }

    public Guid FiscalYearId { get; private set; }
    public Guid VendorId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PurchaseOrderState State { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? RejectedBy { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? IssuedBy { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public string? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public void UpdateDescription(string description)
    {
        if (State != PurchaseOrderState.Draft)
            throw new InvalidOperationException("Only draft purchase orders can be edited.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (description.Trim().Length > 500) throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));
        Description = description.Trim();
    }

    public void Submit(string actor, DateTimeOffset atUtc)
    {
        RequireState(PurchaseOrderState.Draft, "submitted");
        SubmittedBy = NormalizeActor(actor);
        SubmittedAtUtc = atUtc;
        State = PurchaseOrderState.PendingApproval;
    }

    public void Approve(string actor, DateTimeOffset atUtc)
    {
        RequireState(PurchaseOrderState.PendingApproval, "approved");
        ApprovedBy = NormalizeActor(actor);
        ApprovedAtUtc = atUtc;
        State = PurchaseOrderState.Approved;
    }

    public void Reject(string actor, string reason, DateTimeOffset atUtc)
    {
        RequireState(PurchaseOrderState.PendingApproval, "rejected");
        RejectedBy = NormalizeActor(actor);
        RejectedAtUtc = atUtc;
        RejectionReason = NormalizeReason(reason, "Rejection reason");
        State = PurchaseOrderState.Rejected;
    }

    public void Issue(string actor, DateTimeOffset atUtc)
    {
        RequireState(PurchaseOrderState.Approved, "issued");
        IssuedBy = NormalizeActor(actor);
        IssuedAtUtc = atUtc;
        State = PurchaseOrderState.Issued;
    }

    public void Close(string actor, DateTimeOffset atUtc)
    {
        RequireState(PurchaseOrderState.Issued, "closed");
        ClosedBy = NormalizeActor(actor);
        ClosedAtUtc = atUtc;
        State = PurchaseOrderState.Closed;
    }

    public void Cancel(string actor, string reason, DateTimeOffset atUtc)
    {
        if (State is PurchaseOrderState.Closed or PurchaseOrderState.Cancelled or PurchaseOrderState.Rejected)
            throw new InvalidOperationException($"Purchase order cannot be cancelled from state {State}.");
        CancelledBy = NormalizeActor(actor);
        CancelledAtUtc = atUtc;
        CancellationReason = NormalizeReason(reason, "Cancellation reason");
        State = PurchaseOrderState.Cancelled;
    }

    private void RequireState(PurchaseOrderState required, string action)
    {
        if (State != required) throw new InvalidOperationException($"Purchase order cannot be {action} from state {State}.");
    }

    private static string NormalizeActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        if (actor.Trim().Length > 256) throw new ArgumentException("Actor cannot exceed 256 characters.", nameof(actor));
        return actor.Trim();
    }

    private static string NormalizeReason(string reason, string label)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException($"{label} is required.", nameof(reason));
        if (reason.Trim().Length > 1000) throw new ArgumentException($"{label} cannot exceed 1000 characters.", nameof(reason));
        return reason.Trim();
    }
}

public sealed class PurchaseOrderLine : AuditableEntity
{
    private PurchaseOrderLine() { }

    public PurchaseOrderLine(
        Guid purchaseOrderId,
        int lineNumber,
        string description,
        decimal quantity,
        decimal unitCost,
        Guid? budgetItemId = null,
        Guid? financeAccountId = null,
        Guid? departmentId = null,
        Guid? locationId = null)
    {
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("Purchase order is required.", nameof(purchaseOrderId));
        if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        PurchaseOrderId = purchaseOrderId;
        LineNumber = lineNumber;
        Update(description, quantity, unitCost, budgetItemId, financeAccountId, departmentId, locationId);
    }

    public Guid PurchaseOrderId { get; private set; }
    public int LineNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? BudgetItemId { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }

    public void Update(
        string description,
        decimal quantity,
        decimal unitCost,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Line description is required.", nameof(description));
        if (description.Trim().Length > 500) throw new ArgumentException("Line description cannot exceed 500 characters.", nameof(description));
        if (quantity <= 0m) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitCost < 0m) throw new ArgumentOutOfRangeException(nameof(unitCost));
        Description = description.Trim();
        Quantity = quantity;
        UnitCost = unitCost;
        LineTotal = checked(quantity * unitCost);
        BudgetItemId = NormalizeId(budgetItemId, nameof(budgetItemId));
        FinanceAccountId = NormalizeId(financeAccountId, nameof(financeAccountId));
        DepartmentId = NormalizeId(departmentId, nameof(departmentId));
        LocationId = NormalizeId(locationId, nameof(locationId));
    }

    private static Guid? NormalizeId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier cannot be an empty GUID.", parameterName);
        return value;
    }
}
