using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Procurement;

public sealed record ContractOption(Guid Id, string Label);

public sealed record ContractSummary(
    Guid Id,
    string ContractNumber,
    string Name,
    string Vendor,
    ContractState State,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly RenewalNoticeDate,
    bool AutoRenew,
    decimal EstimatedAnnualAmount,
    ContractRenewalStatus? RenewalStatus);

public sealed record ContractRenewalSummary(
    Guid Id,
    DateOnly NoticeDate,
    DateOnly RenewalDate,
    decimal ExpectedAmount,
    ContractRenewalStatus Status,
    string? DecisionBy,
    DateTimeOffset? DecisionAtUtc,
    string? DecisionNote);

public sealed record ContractIndexSnapshot(
    IReadOnlyList<ContractSummary> Contracts,
    IReadOnlyList<ContractOption> Vendors,
    IReadOnlyList<ContractOption> BudgetItems,
    IReadOnlyList<ContractOption> FinanceAccounts);

public sealed record ContractDetailSnapshot(
    ContractSummary Contract,
    string? Description,
    string? BudgetItem,
    string? FinanceAccount,
    IReadOnlyList<ContractRenewalSummary> Renewals);

public sealed class ContractService(LedgerForgeDbContext dbContext)
{
    public async Task<ContractIndexSnapshot> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        await EnsurePendingRenewalsAsync(cancellationToken);
        var vendorNames = await dbContext.Vendors.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var renewalStatus = await dbContext.ContractRenewals.AsNoTracking()
            .GroupBy(x => x.ContractId)
            .Select(x => new { ContractId = x.Key, Latest = x.OrderByDescending(r => r.RenewalDate).Select(r => (ContractRenewalStatus?)r.Status).FirstOrDefault() })
            .ToDictionaryAsync(x => x.ContractId, x => x.Latest, cancellationToken);
        var contracts = await dbContext.Contracts.AsNoTracking().OrderBy(x => x.EndDate).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var summaries = contracts.Select(x => new ContractSummary(
            x.Id,
            x.ContractNumber,
            x.Name,
            vendorNames.TryGetValue(x.VendorId, out var vendor) ? vendor : "Unknown vendor",
            x.State,
            x.StartDate,
            x.EndDate,
            x.RenewalNoticeDate,
            x.AutoRenew,
            x.EstimatedAnnualAmount,
            renewalStatus.TryGetValue(x.Id, out var status) ? status : null)).ToArray();

        return new(
            summaries,
            await ActiveVendorOptions(cancellationToken),
            await CurrentBudgetOptions(cancellationToken),
            await ActiveLookupOptions(dbContext.FinanceAccounts, cancellationToken));
    }

    public async Task<ContractDetailSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsurePendingRenewalsAsync(cancellationToken);
        var contract = await dbContext.Contracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (contract is null) return null;
        var vendor = await dbContext.Vendors.AsNoTracking().Where(x => x.Id == contract.VendorId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Unknown vendor";
        var budgetItem = contract.BudgetItemId is Guid budgetId
            ? await dbContext.BudgetItems.AsNoTracking().Where(x => x.Id == budgetId).Select(x => x.ItemNumber + " · " + x.Description).SingleOrDefaultAsync(cancellationToken)
            : null;
        var financeAccount = contract.FinanceAccountId is Guid accountId
            ? await dbContext.FinanceAccounts.AsNoTracking().Where(x => x.Id == accountId).Select(x => x.Code + " · " + x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        var renewals = await dbContext.ContractRenewals.AsNoTracking()
            .Where(x => x.ContractId == id)
            .OrderByDescending(x => x.RenewalDate)
            .Select(x => new ContractRenewalSummary(
                x.Id, x.NoticeDate, x.RenewalDate, x.ExpectedAmount, x.Status,
                x.DecisionBy, x.DecisionAtUtc, x.DecisionNote))
            .ToListAsync(cancellationToken);
        var summary = new ContractSummary(
            contract.Id, contract.ContractNumber, contract.Name, vendor, contract.State,
            contract.StartDate, contract.EndDate, contract.RenewalNoticeDate, contract.AutoRenew,
            contract.EstimatedAnnualAmount, renewals.FirstOrDefault()?.Status);
        return new(summary, contract.Description, budgetItem, financeAccount, renewals);
    }

    public async Task<Guid> CreateAsync(
        Guid vendorId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        decimal estimatedAnnualAmount,
        bool autoRenew,
        int renewalNoticeDays,
        string? description,
        Guid? budgetItemId,
        Guid? financeAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Vendors.AsNoTracking().AnyAsync(x => x.Id == vendorId && x.IsActive, cancellationToken))
            throw new ArgumentException("Selected vendor does not exist or is inactive.", nameof(vendorId));
        contractNumber = contractNumber?.Trim() ?? string.Empty;
        if (await dbContext.Contracts.AnyAsync(x => x.VendorId == vendorId && x.ContractNumber == contractNumber, cancellationToken))
            throw new InvalidOperationException($"Contract number '{contractNumber}' already exists for the selected vendor.");
        if (budgetItemId is not null && !await dbContext.BudgetItems.AsNoTracking().AnyAsync(x => x.Id == budgetItemId, cancellationToken))
            throw new ArgumentException("Selected budget item does not exist.", nameof(budgetItemId));
        if (financeAccountId is not null && !await dbContext.FinanceAccounts.AsNoTracking().AnyAsync(x => x.Id == financeAccountId && x.IsActive, cancellationToken))
            throw new ArgumentException("Selected finance account does not exist or is inactive.", nameof(financeAccountId));

        var contract = new Contract(
            vendorId, contractNumber, name, startDate, endDate, estimatedAnnualAmount,
            autoRenew, renewalNoticeDays, description, budgetItemId, financeAccountId);
        dbContext.Contracts.Add(contract);
        dbContext.ContractRenewals.Add(new ContractRenewal(
            contract.Id,
            contract.EndDate,
            contract.RenewalNoticeDate,
            contract.EstimatedAnnualAmount));
        await dbContext.SaveChangesAsync(cancellationToken);
        return contract.Id;
    }

    public async Task ActivateAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var contract = await RequireContract(id, cancellationToken);
        contract.Activate(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TerminateAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var contract = await RequireContract(id, cancellationToken);
        contract.Terminate(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DecideRenewalAsync(
        Guid contractId,
        Guid renewalId,
        ContractRenewalStatus status,
        string actor,
        string note,
        CancellationToken cancellationToken = default)
    {
        var renewal = await dbContext.ContractRenewals.SingleOrDefaultAsync(x => x.Id == renewalId && x.ContractId == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Renewal record was not found for the selected contract.");
        renewal.Decide(status, actor, note, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteRenewalAsync(
        Guid contractId,
        Guid renewalId,
        string actor,
        string note,
        CancellationToken cancellationToken = default)
    {
        var renewal = await dbContext.ContractRenewals.SingleOrDefaultAsync(x => x.Id == renewalId && x.ContractId == contractId, cancellationToken)
            ?? throw new KeyNotFoundException("Renewal record was not found for the selected contract.");
        renewal.Complete(actor, note, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsurePendingRenewalsAsync(CancellationToken cancellationToken = default)
    {
        var activeContracts = await dbContext.Contracts
            .Where(x => x.State == ContractState.Active)
            .ToListAsync(cancellationToken);
        if (activeContracts.Count == 0) return;
        var activeIds = activeContracts.Select(x => x.Id).ToArray();
        var existing = await dbContext.ContractRenewals.AsNoTracking()
            .Where(x => activeIds.Contains(x.ContractId))
            .Select(x => new { x.ContractId, x.RenewalDate })
            .ToListAsync(cancellationToken);
        var keys = existing.Select(x => (x.ContractId, x.RenewalDate)).ToHashSet();
        var added = false;
        foreach (var contract in activeContracts)
        {
            if (keys.Contains((contract.Id, contract.EndDate))) continue;
            dbContext.ContractRenewals.Add(new ContractRenewal(contract.Id, contract.EndDate, contract.RenewalNoticeDate, contract.EstimatedAnnualAmount));
            added = true;
        }
        if (added) await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Contract> RequireContract(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Contract ID is required.", nameof(id));
        return await dbContext.Contracts.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Contract was not found.");
    }

    private async Task<IReadOnlyList<ContractOption>> ActiveVendorOptions(CancellationToken cancellationToken)
        => await dbContext.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new ContractOption(x.Id, x.Code + " · " + x.Name)).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<ContractOption>> CurrentBudgetOptions(CancellationToken cancellationToken)
    {
        var currentYearId = await dbContext.FiscalYears.AsNoTracking().Where(x => x.IsCurrent).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (currentYearId is null) return [];
        var versionId = await dbContext.BudgetVersions.AsNoTracking().Where(x => x.FiscalYearId == currentYearId.Value)
            .OrderByDescending(x => x.VersionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (versionId is null) return [];
        return await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.BudgetVersionId == versionId.Value && x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived)
            .OrderBy(x => x.ItemNumber)
            .Select(x => new ContractOption(x.Id, x.ItemNumber + " · " + x.Description)).ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ContractOption>> ActiveLookupOptions<TEntity>(DbSet<TEntity> set, CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
        => await set.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new ContractOption(x.Id, x.Code + " · " + x.Name)).ToListAsync(cancellationToken);
}
