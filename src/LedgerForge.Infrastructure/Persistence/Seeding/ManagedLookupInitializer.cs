using LedgerForge.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Persistence.Seeding;

public sealed class ManagedLookupInitializer(LedgerForgeDbContext dbContext)
{
    public async Task InitializeMissingAsync(CancellationToken cancellationToken = default)
    {
        await AddMissingAsync(dbContext.BudgetSections, DefaultLookupSeeds.BudgetSections, x => new BudgetSection(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.InternalCategories, DefaultLookupSeeds.InternalCategories, x => new InternalCategory(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Priorities, DefaultLookupSeeds.Priorities, x => new PriorityLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.Frequencies, DefaultLookupSeeds.Frequencies, x => new Frequency(x.Code, x.Name, x.SortOrder), cancellationToken);
        await AddMissingAsync(dbContext.PurchaseTypes, DefaultLookupSeeds.PurchaseTypes, x => new PurchaseTypeLookup(x.Code, x.Name, x.SortOrder), cancellationToken);
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
    }

    private async Task AddMissingNeedLevelsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = (await dbContext.NeedLevels.Select(x => x.Code).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in DefaultLookupSeeds.NeedLevels)
        {
            if (existingCodes.Add(seed.Code)) dbContext.NeedLevels.Add(new NeedLevel(seed.Code, seed.Name, seed.NumericValue, seed.SortOrder));
        }
    }

    private static async Task AddMissingAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<LookupSeed> seeds,
        Func<LookupSeed, TEntity> factory,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        var existingCodes = (await set.Select(x => x.Code).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            if (existingCodes.Add(seed.Code)) set.Add(factory(seed));
        }
    }
}
