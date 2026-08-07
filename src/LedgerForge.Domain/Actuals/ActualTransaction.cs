using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Actuals;

public enum ActualTransactionKind
{
    Manual,
    Invoice,
    Adjustment,
    Reversal
}

public sealed class ActualTransaction : AuditableEntity
{
    private ActualTransaction() { }

    public ActualTransaction(
        Guid fiscalYearId,
        DateOnly transactionDate,
        decimal amount,
        string description,
        ActualTransactionKind kind = ActualTransactionKind.Manual,
        string? sourceReference = null,
        Guid? budgetItemId = null,
        Guid? financeAccountId = null,
        Guid? departmentId = null,
        Guid? locationId = null,
        Guid? fiscalPeriodId = null)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Normal actual transactions must have a positive amount.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (kind == ActualTransactionKind.Reversal) throw new ArgumentException("Use CreateReversal to create reversal transactions.", nameof(kind));

        FiscalYearId = fiscalYearId;
        TransactionDate = transactionDate;
        Amount = amount;
        Description = NormalizeRequired(description, 500, nameof(description));
        Kind = kind;
        SourceReference = NormalizeOptional(sourceReference, 200, nameof(sourceReference));
        BudgetItemId = NormalizeId(budgetItemId, nameof(budgetItemId));
        FinanceAccountId = NormalizeId(financeAccountId, nameof(financeAccountId));
        DepartmentId = NormalizeId(departmentId, nameof(departmentId));
        LocationId = NormalizeId(locationId, nameof(locationId));
        FiscalPeriodId = NormalizeId(fiscalPeriodId, nameof(fiscalPeriodId));
    }

    public Guid FiscalYearId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public ActualTransactionKind Kind { get; private set; }
    public string? SourceReference { get; private set; }
    public Guid? BudgetItemId { get; private set; }
    public Guid? FinanceAccountId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? FiscalPeriodId { get; private set; }
    public Guid? ReversesTransactionId { get; private set; }
    public string? ReversalReason { get; private set; }

    public ActualTransaction CreateReversal(DateOnly reversalDate, string reason, Guid? reversalFiscalPeriodId = null)
    {
        if (Kind == ActualTransactionKind.Reversal)
            throw new InvalidOperationException("A reversal transaction cannot itself be reversed through this method.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        return new ActualTransaction
        {
            FiscalYearId = FiscalYearId,
            TransactionDate = reversalDate,
            Amount = -Amount,
            Description = Description,
            Kind = ActualTransactionKind.Reversal,
            SourceReference = SourceReference,
            BudgetItemId = BudgetItemId,
            FinanceAccountId = FinanceAccountId,
            DepartmentId = DepartmentId,
            LocationId = LocationId,
            FiscalPeriodId = NormalizeId(reversalFiscalPeriodId, nameof(reversalFiscalPeriodId)),
            ReversesTransactionId = Id,
            ReversalReason = NormalizeRequired(reason, 1000, nameof(reason))
        };
    }

    private static Guid? NormalizeId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier cannot be an empty GUID.", parameterName);
        return value;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return NormalizeRequired(value, maxLength, parameterName);
    }
}
