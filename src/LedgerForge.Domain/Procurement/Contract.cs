using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Procurement;

public enum ContractState
{
    Draft,
    Active,
    Expired,
    Terminated
}

public enum ContractRenewalStatus
{
    PendingReview,
    Renew,
    Renegotiate,
    Terminate,
    Deferred,
    Completed
}

public sealed class Contract : AuditableEntity
{
    private Contract() { }

    public Contract(
        Guid vendorId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        decimal estimatedAnnualAmount,
        bool autoRenew = false,
        int renewalNoticeDays = 60,
        string? description = null,
        Guid? budgetItemId = null,
        Guid? financeAccountId = null)
    {
        if (vendorId == Guid.Empty) throw new ArgumentException("Vendor is required.", nameof(vendorId));
        ContractNumber = Required(contractNumber, 100, nameof(contractNumber));
        Name = Required(name, 250, nameof(name));
        if (endDate < startDate) throw new ArgumentException("Contract end date cannot be before the start date.", nameof(endDate));
        if (estimatedAnnualAmount < 0m) throw new ArgumentOutOfRangeException(nameof(estimatedAnnualAmount));
        if (renewalNoticeDays < 0 || renewalNoticeDays > 730) throw new ArgumentOutOfRangeException(nameof(renewalNoticeDays));
        if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item ID cannot be empty.", nameof(budgetItemId));
        if (financeAccountId == Guid.Empty) throw new ArgumentException("Finance account ID cannot be empty.", nameof(financeAccountId));

        VendorId = vendorId;
        StartDate = startDate;
        EndDate = endDate;
        EstimatedAnnualAmount = estimatedAnnualAmount;
        AutoRenew = autoRenew;
        RenewalNoticeDays = renewalNoticeDays;
        Description = Optional(description, 2000, nameof(description));
        BudgetItemId = budgetItemId;
        FinanceAccountId = financeAccountId;
        State = ContractState.Draft;
    }

    public Guid VendorId { get; private set; }
    public string ContractNumber { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal EstimatedAnnualAmount { get; private set; }
    public bool AutoRenew { get; private set; }
    public int RenewalNoticeDays { get; private set; }
    public Guid? BudgetItemId { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public ContractState State { get; private set; }
    public string? ActivatedBy { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public string? TerminatedBy { get; private set; }
    public DateTimeOffset? TerminatedAtUtc { get; private set; }
    public string? TerminationReason { get; private set; }

    public DateOnly RenewalNoticeDate => EndDate.AddDays(-RenewalNoticeDays);

    public void Activate(string actor, DateTimeOffset atUtc)
    {
        if (State != ContractState.Draft) throw new InvalidOperationException($"Contract cannot be activated from state {State}.");
        ActivatedBy = Actor(actor);
        ActivatedAtUtc = atUtc;
        State = ContractState.Active;
    }

    public void MarkExpired(DateOnly asOfDate)
    {
        if (State != ContractState.Active) throw new InvalidOperationException($"Contract cannot expire from state {State}.");
        if (asOfDate <= EndDate) throw new InvalidOperationException("Contract cannot be expired before its end date has passed.");
        State = ContractState.Expired;
    }

    public void Terminate(string actor, string reason, DateTimeOffset atUtc)
    {
        if (State is ContractState.Terminated or ContractState.Expired)
            throw new InvalidOperationException($"Contract cannot be terminated from state {State}.");
        TerminatedBy = Actor(actor);
        TerminatedAtUtc = atUtc;
        TerminationReason = Required(reason, 1000, nameof(reason));
        State = ContractState.Terminated;
    }

    private static string Actor(string actor) => Required(actor, 256, nameof(actor));

    private static string Required(string value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int max, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, parameterName);
}

public sealed class ContractRenewal : AuditableEntity
{
    private ContractRenewal() { }

    public ContractRenewal(Guid contractId, DateOnly renewalDate, DateOnly noticeDate, decimal expectedAmount)
    {
        if (contractId == Guid.Empty) throw new ArgumentException("Contract is required.", nameof(contractId));
        if (noticeDate > renewalDate) throw new ArgumentException("Notice date cannot be after the renewal date.", nameof(noticeDate));
        if (expectedAmount < 0m) throw new ArgumentOutOfRangeException(nameof(expectedAmount));
        ContractId = contractId;
        RenewalDate = renewalDate;
        NoticeDate = noticeDate;
        ExpectedAmount = expectedAmount;
        Status = ContractRenewalStatus.PendingReview;
    }

    public Guid ContractId { get; private set; }
    public DateOnly RenewalDate { get; private set; }
    public DateOnly NoticeDate { get; private set; }
    public decimal ExpectedAmount { get; private set; }
    public ContractRenewalStatus Status { get; private set; }
    public string? DecisionBy { get; private set; }
    public DateTimeOffset? DecisionAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }

    public void Decide(ContractRenewalStatus status, string actor, string note, DateTimeOffset atUtc)
    {
        if (Status == ContractRenewalStatus.Completed) throw new InvalidOperationException("Completed renewal decisions cannot be changed.");
        if (status is ContractRenewalStatus.PendingReview or ContractRenewalStatus.Completed)
            throw new ArgumentOutOfRangeException(nameof(status), "Decision must be Renew, Renegotiate, Terminate, or Deferred.");
        DecisionBy = Required(actor, 256, nameof(actor));
        DecisionAtUtc = atUtc;
        DecisionNote = Required(note, 2000, nameof(note));
        Status = status;
    }

    public void Complete(string actor, string note, DateTimeOffset atUtc)
    {
        if (Status is ContractRenewalStatus.PendingReview or ContractRenewalStatus.Deferred)
            throw new InvalidOperationException("A concrete renewal decision is required before completion.");
        if (Status == ContractRenewalStatus.Completed) throw new InvalidOperationException("Renewal decision is already completed.");
        DecisionBy = Required(actor, 256, nameof(actor));
        DecisionAtUtc = atUtc;
        DecisionNote = Required(note, 2000, nameof(note));
        Status = ContractRenewalStatus.Completed;
    }

    private static string Required(string value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }
}
