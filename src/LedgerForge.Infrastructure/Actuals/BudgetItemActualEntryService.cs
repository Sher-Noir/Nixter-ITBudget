using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Actuals;

public sealed record BudgetItemActualEntryOptions(
    IReadOnlyList<ActualEntryOption> FinanceAccounts,
    IReadOnlyList<ActualEntryOption> Departments,
    IReadOnlyList<ActualEntryOption> Locations);

public sealed class BudgetItemActualEntryService(LedgerForgeDbContext dbContext)
{
    public async Task<BudgetItemActualEntryOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
        => new(
            await LoadOptionsAsync(dbContext.FinanceAccounts, cancellationToken),
            await LoadOptionsAsync(dbContext.Departments, cancellationToken),
            await LoadOptionsAsync(dbContext.Locations, cancellationToken));

    public async Task<Guid> PostAsync(
        Guid budgetItemId,
        DateOnly transactionDate,
        decimal amount,
        string description,
        string? sourceReference,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item ID is required.", nameof(budgetItemId));
        var item = await dbContext.BudgetItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == budgetItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");
        var fiscalYear = await dbContext.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == item.FiscalYearId, cancellationToken);
        if (fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException($"{fiscalYear.DisplayName} is {fiscalYear.Status} and cannot accept new actuals.");
        if (transactionDate < fiscalYear.StartDate || transactionDate > fiscalYear.EndDate)
            throw new InvalidOperationException($"Transaction date must fall within {fiscalYear.DisplayName}.");

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

    private static async Task<IReadOnlyList<ActualEntryOption>> LoadOptionsAsync<TEntity>(
        DbSet<TEntity> set,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
        => await set.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ActualEntryOption(x.Id, x.Code + " · " + x.Name))
            .ToListAsync(cancellationToken);

    private static async Task RequireActiveLookupAsync<TEntity>(
        DbSet<TEntity> set,
        Guid? id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (id is null) return;
        if (id == Guid.Empty) throw new ArgumentException($"Selected {label} ID cannot be empty.", nameof(id));
        if (!await set.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, cancellationToken))
            throw new InvalidOperationException($"Selected {label} does not exist or is inactive.");
    }
}
