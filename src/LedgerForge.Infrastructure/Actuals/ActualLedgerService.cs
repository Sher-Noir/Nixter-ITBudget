using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Actuals;

public sealed record ActualFiscalYearOption(Guid Id, string Name, bool IsCurrent);
public sealed record ActualEntryOption(Guid Id, string Label);

public sealed record ActualTransactionSummary(
    Guid Id,
    DateOnly TransactionDate,
    decimal Amount,
    ActualTransactionKind Kind,
    string Description,
    string? SourceReference,
    string? BudgetItem,
    string? FinanceAccount,
    string? Department,
    string? Location,
    string? FiscalPeriod,
    Guid? ReversesTransactionId,
    string? ReversalReason,
    bool CanReverse);

public sealed record ActualLedgerSnapshot(
    IReadOnlyList<ActualFiscalYearOption> FiscalYears,
    Guid? SelectedFiscalYearId,
    IReadOnlyList<ActualEntryOption> BudgetItems,
    IReadOnlyList<ActualEntryOption> FinanceAccounts,
    IReadOnlyList<ActualEntryOption> Departments,
    IReadOnlyList<ActualEntryOption> Locations,
    IReadOnlyList<ActualEntryOption> FiscalPeriods,
    IReadOnlyList<ActualTransactionSummary> Transactions,
    decimal NetActualTotal);

public sealed class ActualLedgerService(LedgerForgeDbContext dbContext)
{
    public async Task<ActualLedgerSnapshot> GetAsync(
        Guid? fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears
            .AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new ActualFiscalYearOption(x.Id, x.DisplayName, x.IsCurrent))
            .ToListAsync(cancellationToken);

        var selectedFiscalYearId = ResolveFiscalYear(years, fiscalYearId);
        if (selectedFiscalYearId is null)
            return new(years, null, [], [], [], [], [], [], 0m);

        var latestVersionId = await dbContext.BudgetVersions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == selectedFiscalYearId.Value)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        IReadOnlyList<ActualEntryOption> budgetItems = latestVersionId is null
            ? []
            : await dbContext.BudgetItems
                .AsNoTracking()
                .Where(x => x.FiscalYearId == selectedFiscalYearId.Value &&
                            x.BudgetVersionId == latestVersionId.Value &&
                            x.Status != BudgetItemStatus.Cancelled &&
                            x.Status != BudgetItemStatus.Archived)
                .OrderBy(x => x.ItemNumber)
                .Select(x => new ActualEntryOption(x.Id, x.ItemNumber + " · " + x.Description))
                .ToListAsync(cancellationToken);

        var financeAccounts = await dbContext.FinanceAccounts
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ActualEntryOption(x.Id, x.Code + " · " + x.Name))
            .ToListAsync(cancellationToken);

        var departments = await dbContext.Departments
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ActualEntryOption(x.Id, x.Code + " · " + x.Name))
            .ToListAsync(cancellationToken);

        var locations = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ActualEntryOption(x.Id, x.Code + " · " + x.Name))
            .ToListAsync(cancellationToken);

        var transactionRows = await dbContext.ActualTransactions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == selectedFiscalYearId.Value)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.TransactionDate,
                x.Amount,
                x.Kind,
                x.Description,
                x.SourceReference,
                x.BudgetItemId,
                x.FinanceAccountId,
                x.DepartmentId,
                x.LocationId,
                x.ReversesTransactionId,
                x.ReversalReason
            })
            .ToListAsync(cancellationToken);

        var budgetItemNames = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedFiscalYearId.Value)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        var reversedIds = transactionRows
            .Where(x => x.ReversesTransactionId is not null)
            .Select(x => x.ReversesTransactionId!.Value)
            .ToHashSet();

        var transactions = transactionRows.Select(x => new ActualTransactionSummary(
            x.Id,
            x.TransactionDate,
            x.Amount,
            x.Kind,
            x.Description,
            x.SourceReference,
            NameFor(x.BudgetItemId, budgetItemNames),
            NameFor(x.FinanceAccountId, accountNames),
            NameFor(x.DepartmentId, departmentNames),
            NameFor(x.LocationId, locationNames),
            null,
            x.ReversesTransactionId,
            x.ReversalReason,
            x.Kind != ActualTransactionKind.Reversal && !reversedIds.Contains(x.Id)))
            .ToArray();

        return new(
            years,
            selectedFiscalYearId,
            budgetItems,
            financeAccounts,
            departments,
            locations,
            [],
            transactions,
            transactions.Sum(x => x.Amount));
    }

    public async Task<Guid> PostManualAsync(
        Guid fiscalYearId,
        DateOnly transactionDate,
        decimal amount,
        string description,
        string? sourceReference,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        // fiscalPeriodId is intentionally ignored for source/binary compatibility with
        // existing callers. Fiscal periods are no longer part of the active ledger model.
        _ = fiscalPeriodId;
        var fiscalYear = await RequireFiscalYearAsync(fiscalYearId, transactionDate, cancellationToken);
        if (fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException($"{fiscalYear.DisplayName} is {fiscalYear.Status} and cannot accept actual transactions.");

        if (budgetItemId is not null)
        {
            if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item ID cannot be empty.", nameof(budgetItemId));
            if (!await dbContext.BudgetItems.AnyAsync(x => x.Id == budgetItemId && x.FiscalYearId == fiscalYear.Id, cancellationToken))
                throw new ArgumentException("Selected budget item does not belong to the selected fiscal year.", nameof(budgetItemId));
        }

        await RequireActiveLookupAsync(dbContext.FinanceAccounts, financeAccountId, "finance account", cancellationToken);
        await RequireActiveLookupAsync(dbContext.Departments, departmentId, "department", cancellationToken);
        await RequireActiveLookupAsync(dbContext.Locations, locationId, "location", cancellationToken);

        var transaction = new ActualTransaction(
            fiscalYear.Id,
            transactionDate,
            amount,
            description,
            ActualTransactionKind.Manual,
            sourceReference,
            budgetItemId,
            financeAccountId,
            departmentId,
            locationId,
            fiscalPeriodId: null);

        dbContext.ActualTransactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transaction.Id;
    }

    public async Task<Guid> ReverseAsync(
        Guid transactionId,
        DateOnly reversalDate,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Actual transaction ID is required.", nameof(transactionId));

        var original = await dbContext.ActualTransactions
            .SingleOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Actual transaction was not found.");

        if (original.Kind == ActualTransactionKind.Reversal)
            throw new InvalidOperationException("Reversal transactions cannot be reversed again.");
        if (await dbContext.ActualTransactions.AnyAsync(x => x.ReversesTransactionId == original.Id, cancellationToken))
            throw new InvalidOperationException("This actual transaction has already been reversed.");
        if (reversalDate < original.TransactionDate)
            throw new InvalidOperationException("A reversal cannot be dated before the original transaction.");

        var fiscalYear = await RequireFiscalYearAsync(original.FiscalYearId, reversalDate, cancellationToken);
        if (fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException($"{fiscalYear.DisplayName} is {fiscalYear.Status} and cannot accept reversal transactions.");
        var reversal = original.CreateReversal(reversalDate, reason, reversalFiscalPeriodId: null);

        dbContext.ActualTransactions.Add(reversal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return reversal.Id;
    }

    private async Task<FiscalYear> RequireFiscalYearAsync(
        Guid fiscalYearId,
        DateOnly transactionDate,
        CancellationToken cancellationToken)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        var year = await dbContext.FiscalYears.SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        if (transactionDate < year.StartDate || transactionDate > year.EndDate)
            throw new InvalidOperationException("Transaction date must fall within the selected fiscal year.");
        return year;
    }

    private static async Task RequireActiveLookupAsync<TEntity>(
        DbSet<TEntity> set,
        Guid? id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (id is null) return;
        if (id == Guid.Empty) throw new ArgumentException($"Selected {label} ID cannot be empty.", nameof(id));
        var exists = await set.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (!exists) throw new InvalidOperationException($"Selected {label} does not exist or is inactive.");
    }

    private static Guid? ResolveFiscalYear(IReadOnlyList<ActualFiscalYearOption> years, Guid? requested)
    {
        if (requested is not null && years.Any(x => x.Id == requested)) return requested;
        return years.FirstOrDefault(x => x.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
    }

    private static string? NameFor(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is Guid value && names.TryGetValue(value, out var name) ? name : null;
}