using Crch.ItBudget.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Infrastructure.Persistence.Seeding;

public sealed class ManagedLookupInitializer(ItBudgetDbContext dbContext)
{
    public async Task InitializeMissingAsync(CancellationToken cancellationToken = default)
    {
        await AddMissingAsync(dbContext.BudgetSections, WorkbookLookupSeeds.BudgetSections, x => new BudgetSection(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.FinanceTypes, WorkbookLookupSeeds.FinanceTypes, x => new FinanceType(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.FinanceCategories, WorkbookLookupSeeds.FinanceCategories, x => new FinanceCategory(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.InternalCategories, WorkbookLookupSeeds.InternalCategories, x => new InternalCategory(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Departments, WorkbookLookupSeeds.Departments, x => new Department(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Locations, WorkbookLookupSeeds.Locations, x => new Location(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Priorities, WorkbookLookupSeeds.Priorities, x => new PriorityLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Frequencies, WorkbookLookupSeeds.Frequencies, x => new Frequency(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.PurchaseTypes, WorkbookLookupSeeds.PurchaseTypes, x => new PurchaseTypeLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.UnitsOfMeasure, WorkflowLookupSeeds.UnitsOfMeasure, x => new UnitOfMeasure(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.DocumentTypes, WorkflowLookupSeeds.DocumentTypes, x => new DocumentType(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.ApprovalStatuses, WorkflowLookupSeeds.ApprovalStatuses, x => new ApprovalStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.TransactionTypes, WorkflowLookupSeeds.TransactionTypes, x => new TransactionTypeLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.PurchaseOrderStatuses, WorkflowLookupSeeds.PurchaseOrderStatuses, x => new PurchaseOrderStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.InvoiceStatuses, WorkflowLookupSeeds.InvoiceStatuses, x => new InvoiceStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.RenewalStatuses, WorkflowLookupSeeds.RenewalStatuses, x => new RenewalStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.ContractStatuses, WorkflowLookupSeeds.ContractStatuses, x => new ContractStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken);

        await AddMissingNeedLevelsAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AddMissingFinanceAccountsAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddMissingNeedLevelsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.NeedLevels
                .Select(x => x.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in WorkbookLookupSeeds.NeedLevels)
        {
            if (existingCodes.Add(seed.Code))
            {
                dbContext.NeedLevels.Add(new NeedLevel(seed.Code, seed.Name, seed.NumericValue, seed.SortOrder));
            }
        }
    }

    private async Task AddMissingFinanceAccountsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.FinanceAccounts
                .Select(x => x.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categoryIds = await dbContext.FinanceCategories
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var seed in WorkbookLookupSeeds.FinanceAccounts)
        {
            if (!existingCodes.Add(seed.Code)) continue;

            if (!categoryIds.TryGetValue(seed.FinanceCategoryCode, out var categoryId))
            {
                throw new InvalidOperationException(
                    $"Finance category '{seed.FinanceCategoryCode}' must exist before account '{seed.Code}' can be initialized.");
            }

            dbContext.FinanceAccounts.Add(
                new FinanceAccount(seed.Code, seed.Name, categoryId, seed.SortOrder));
        }
    }

    private static async Task AddMissingAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<LookupSeed> seeds,
        Func<LookupSeed, TEntity> factory,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        var existingCodes = (await set
                .Select(x => x.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            if (existingCodes.Add(seed.Code))
            {
                set.Add(factory(seed));
            }
        }
    }
}
