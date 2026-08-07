namespace LedgerForge.Infrastructure.Persistence.Seeding;

public static class WorkflowLookupSeeds
{
    public static IReadOnlyList<LookupSeed> UnitsOfMeasure { get; } =
    [
        new("EACH", "Each", 10), new("LICENSE", "License", 20), new("USER", "User", 30),
        new("DEVICE", "Device", 40), new("LOCATION", "Location", 50), new("MONTH", "Month", 60),
        new("HOUR", "Hour", 70), new("DAY", "Day", 80), new("OTHER", "Other", 90)
    ];

    public static IReadOnlyList<LookupSeed> DocumentTypes { get; } =
    [
        new("RECEIPT", "Receipt", 10), new("INVOICE", "Invoice", 20), new("PURCHASE_ORDER", "Purchase Order", 30),
        new("QUOTE", "Quote", 40), new("CONTRACT", "Contract", 50), new("RENEWAL_NOTICE", "Renewal Notice", 60),
        new("STATEMENT_OF_WORK", "Statement of Work", 70), new("APPROVAL", "Approval", 80), new("EMAIL", "Email", 90),
        new("SPREADSHEET", "Spreadsheet", 100), new("SUPPORTING_DOCUMENTATION", "Supporting Documentation", 110), new("OTHER", "Other", 120)
    ];

    public static IReadOnlyList<LookupSeed> ApprovalStatuses { get; } =
    [
        new("DRAFT", "Draft", 10), new("NEEDS_INFORMATION", "Needs Information", 20), new("SUBMITTED", "Submitted", 30),
        new("IN_REVIEW", "In Review", 40), new("APPROVED", "Approved", 50), new("DENIED", "Denied", 60),
        new("DEFERRED", "Deferred", 70), new("WITHDRAWN", "Withdrawn", 80), new("CANCELLED", "Cancelled", 90)
    ];

    public static IReadOnlyList<LookupSeed> TransactionTypes { get; } =
    [
        new("INVOICE", "Invoice", 10), new("PAYMENT", "Payment", 20), new("CREDIT", "Credit", 30),
        new("REFUND", "Refund", 40), new("ACCRUAL", "Accrual", 50), new("ADJUSTMENT", "Adjustment", 60),
        new("COMMITMENT_RELEASE", "Commitment Release", 70), new("OTHER", "Other", 80)
    ];

    public static IReadOnlyList<LookupSeed> PurchaseOrderStatuses { get; } =
    [
        new("DRAFT", "Draft", 10), new("REQUESTED", "Requested", 20), new("PENDING_APPROVAL", "Pending Approval", 30),
        new("APPROVED", "Approved", 40), new("ISSUED", "Issued", 50), new("OPEN", "Open", 60),
        new("PARTIALLY_INVOICED", "Partially Invoiced", 70), new("FULLY_INVOICED", "Fully Invoiced", 80),
        new("CLOSED", "Closed", 90), new("CANCELLED", "Cancelled", 100), new("EXPIRED", "Expired", 110)
    ];

    public static IReadOnlyList<LookupSeed> InvoiceStatuses { get; } =
    [
        new("RECEIVED", "Received", 10), new("NEEDS_REVIEW", "Needs Review", 20), new("APPROVED", "Approved", 30),
        new("SUBMITTED_TO_FINANCE", "Submitted to Finance", 40), new("PAID", "Paid", 50), new("PARTIALLY_PAID", "Partially Paid", 60),
        new("DISPUTED", "Disputed", 70), new("VOID", "Void", 80), new("ARCHIVED", "Archived", 90)
    ];

    public static IReadOnlyList<LookupSeed> RenewalStatuses { get; } =
    [
        new("UPCOMING", "Upcoming", 10), new("DUE_WITHIN_90_DAYS", "Due Within 90 Days", 20), new("DUE_WITHIN_60_DAYS", "Due Within 60 Days", 30),
        new("DUE_WITHIN_30_DAYS", "Due Within 30 Days", 40), new("OVERDUE", "Overdue", 50), new("DECISION_PENDING", "Decision Pending", 60),
        new("RENEWING", "Renewing", 70), new("RENEWED", "Renewed", 80), new("NON_RENEWING", "Non-Renewing", 90),
        new("REPLACED", "Replaced", 100), new("CANCELLED", "Cancelled", 110)
    ];

    public static IReadOnlyList<LookupSeed> ContractStatuses { get; } =
    [
        new("DRAFT", "Draft", 10), new("UNDER_REVIEW", "Under Review", 20), new("ACTIVE", "Active", 30),
        new("RENEWAL_PENDING", "Renewal Pending", 40), new("NON_RENEWING", "Non-Renewing", 50), new("EXPIRED", "Expired", 60),
        new("TERMINATED", "Terminated", 70), new("ARCHIVED", "Archived", 80)
    ];
}
