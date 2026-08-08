using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Budgeting;

public enum BudgetAmendmentState
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled
}

public sealed class BudgetAmendment : AuditableEntity
{
    private BudgetAmendment() { }

    public BudgetAmendment(
        Guid fiscalYearId,
        Guid budgetVersionId,
        Guid budgetItemId,
        decimal amountDelta,
        string reason)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (budgetVersionId == Guid.Empty) throw new ArgumentException("Budget version is required.", nameof(budgetVersionId));
        if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item is required.", nameof(budgetItemId));
        if (amountDelta == 0m) throw new ArgumentOutOfRangeException(nameof(amountDelta), "Amendment delta cannot be zero.");
        FiscalYearId = fiscalYearId;
        BudgetVersionId = budgetVersionId;
        BudgetItemId = budgetItemId;
        AmountDelta = amountDelta;
        Reason = Required(reason, 2000, nameof(reason));
        State = BudgetAmendmentState.Draft;
    }

    public Guid FiscalYearId { get; private set; }
    public Guid BudgetVersionId { get; private set; }
    public Guid BudgetItemId { get; private set; }
    public decimal AmountDelta { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public BudgetAmendmentState State { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? DecisionBy { get; private set; }
    public DateTimeOffset? DecisionAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public decimal? ResultingRevisedTotal { get; private set; }
    public string? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public void Submit(string actor, DateTimeOffset atUtc)
    {
        if (State != BudgetAmendmentState.Draft) throw new InvalidOperationException($"Amendment cannot be submitted from state {State}.");
        SubmittedBy = Actor(actor);
        SubmittedAtUtc = atUtc;
        State = BudgetAmendmentState.PendingApproval;
    }

    public void Approve(string actor, decimal resultingRevisedTotal, string? note, DateTimeOffset atUtc)
    {
        if (State != BudgetAmendmentState.PendingApproval) throw new InvalidOperationException($"Amendment cannot be approved from state {State}.");
        if (resultingRevisedTotal < 0m) throw new ArgumentOutOfRangeException(nameof(resultingRevisedTotal));
        DecisionBy = Actor(actor);
        DecisionAtUtc = atUtc;
        DecisionNote = Optional(note, 2000, nameof(note));
        ResultingRevisedTotal = resultingRevisedTotal;
        State = BudgetAmendmentState.Approved;
    }

    public void Reject(string actor, string note, DateTimeOffset atUtc)
    {
        if (State != BudgetAmendmentState.PendingApproval) throw new InvalidOperationException($"Amendment cannot be rejected from state {State}.");
        DecisionBy = Actor(actor);
        DecisionAtUtc = atUtc;
        DecisionNote = Required(note, 2000, nameof(note));
        State = BudgetAmendmentState.Rejected;
    }

    public void Cancel(string actor, string reason, DateTimeOffset atUtc)
    {
        if (State is BudgetAmendmentState.Approved or BudgetAmendmentState.Rejected or BudgetAmendmentState.Cancelled)
            throw new InvalidOperationException($"Amendment cannot be cancelled from state {State}.");
        CancelledBy = Actor(actor);
        CancelledAtUtc = atUtc;
        CancellationReason = Required(reason, 2000, nameof(reason));
        State = BudgetAmendmentState.Cancelled;
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
