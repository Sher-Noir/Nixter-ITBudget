using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Procurement;

public enum InvoiceState
{
    Draft,
    PendingApproval,
    Approved,
    Posted,
    Cancelled,
    Rejected
}

public sealed class Invoice : AuditableEntity
{
    private Invoice() { }

    public Invoice(
        Guid fiscalYearId,
        Guid vendorId,
        string invoiceNumber,
        DateOnly invoiceDate,
        string description,
        decimal totalAmount,
        Guid? purchaseOrderId = null)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor is required.", nameof(vendorId));
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new ArgumentException("Invoice number is required.", nameof(invoiceNumber));
        if (invoiceNumber.Trim().Length > 100) throw new ArgumentException("Invoice number cannot exceed 100 characters.", nameof(invoiceNumber));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (description.Trim().Length > 500) throw new ArgumentException("Description cannot exceed 500 characters.", nameof(description));
        if (totalAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(totalAmount));
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("Purchase order ID cannot be empty.", nameof(purchaseOrderId));

        FiscalYearId = fiscalYearId;
        VendorId = vendorId;
        InvoiceNumber = invoiceNumber.Trim();
        InvoiceDate = invoiceDate;
        Description = description.Trim();
        TotalAmount = totalAmount;
        PurchaseOrderId = purchaseOrderId;
        State = InvoiceState.Draft;
    }

    public Guid FiscalYearId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public InvoiceState State { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? RejectedBy { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? PostedBy { get; private set; }
    public DateTimeOffset? PostedAtUtc { get; private set; }
    public string? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public void Submit(string actor, DateTimeOffset atUtc)
    {
        RequireState(InvoiceState.Draft, "submitted");
        SubmittedBy = NormalizeActor(actor);
        SubmittedAtUtc = atUtc;
        State = InvoiceState.PendingApproval;
    }

    public void Approve(string actor, DateTimeOffset atUtc)
    {
        RequireState(InvoiceState.PendingApproval, "approved");
        ApprovedBy = NormalizeActor(actor);
        ApprovedAtUtc = atUtc;
        State = InvoiceState.Approved;
    }

    public void Reject(string actor, string reason, DateTimeOffset atUtc)
    {
        RequireState(InvoiceState.PendingApproval, "rejected");
        RejectedBy = NormalizeActor(actor);
        RejectedAtUtc = atUtc;
        RejectionReason = NormalizeReason(reason, "Rejection reason");
        State = InvoiceState.Rejected;
    }

    public void MarkPosted(string actor, DateTimeOffset atUtc)
    {
        RequireState(InvoiceState.Approved, "posted");
        PostedBy = NormalizeActor(actor);
        PostedAtUtc = atUtc;
        State = InvoiceState.Posted;
    }

    public void Cancel(string actor, string reason, DateTimeOffset atUtc)
    {
        if (State is InvoiceState.Posted or InvoiceState.Cancelled or InvoiceState.Rejected)
            throw new InvalidOperationException($"Invoice cannot be cancelled from state {State}.");
        CancelledBy = NormalizeActor(actor);
        CancelledAtUtc = atUtc;
        CancellationReason = NormalizeReason(reason, "Cancellation reason");
        State = InvoiceState.Cancelled;
    }

    private void RequireState(InvoiceState required, string action)
    {
        if (State != required) throw new InvalidOperationException($"Invoice cannot be {action} from state {State}.");
    }

    private static string NormalizeActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        var normalized = actor.Trim();
        if (normalized.Length > 256) throw new ArgumentException("Actor cannot exceed 256 characters.", nameof(actor));
        return normalized;
    }

    private static string NormalizeReason(string reason, string label)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException($"{label} is required.", nameof(reason));
        var normalized = reason.Trim();
        if (normalized.Length > 1000) throw new ArgumentException($"{label} cannot exceed 1000 characters.", nameof(reason));
        return normalized;
    }
}

public sealed class InvoiceAllocation : AuditableEntity
{
    private InvoiceAllocation() { }

    public InvoiceAllocation(
        Guid invoiceId,
        int lineNumber,
        string description,
        decimal amount,
        Guid? budgetItemId = null,
        Guid? financeAccountId = null,
        Guid? departmentId = null,
        Guid? locationId = null,
        Guid? fiscalPeriodId = null)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice is required.", nameof(invoiceId));
        if (lineNumber < 1) throw new ArgumentOutOfRangeException(nameof(lineNumber));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Allocation description is required.", nameof(description));
        if (description.Trim().Length > 500) throw new ArgumentException("Allocation description cannot exceed 500 characters.", nameof(description));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));

        InvoiceId = invoiceId;
        LineNumber = lineNumber;
        Description = description.Trim();
        Amount = amount;
        BudgetItemId = NormalizeId(budgetItemId, nameof(budgetItemId));
        FinanceAccountId = NormalizeId(financeAccountId, nameof(financeAccountId));
        DepartmentId = NormalizeId(departmentId, nameof(departmentId));
        LocationId = NormalizeId(locationId, nameof(locationId));
        FiscalPeriodId = NormalizeId(fiscalPeriodId, nameof(fiscalPeriodId));
    }

    public Guid InvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public Guid? BudgetItemId { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? FiscalPeriodId { get; private set; }

    public void AssignFiscalPeriod(Guid fiscalPeriodId)
    {
        if (fiscalPeriodId == Guid.Empty) throw new ArgumentException("Fiscal period ID cannot be empty.", nameof(fiscalPeriodId));
        FiscalPeriodId = fiscalPeriodId;
    }

    private static Guid? NormalizeId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier cannot be an empty GUID.", parameterName);
        return value;
    }
}
