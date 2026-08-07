using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Reporting;

public sealed record RenewalProjectionItem(
    Guid Id,
    string Source,
    Guid? BudgetItemId,
    Guid? ContractId,
    string Reference,
    string Description,
    string? Vendor,
    DateOnly RenewalDate,
    DateOnly? NoticeDate,
    decimal EstimatedAmount,
    string Status);

public sealed class RenewalProjectionService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<RenewalProjectionItem>> GetForFiscalYearAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = await dbContext.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");

        var contracts = await dbContext.Contracts.AsNoTracking()
            .Where(x => x.State != ContractState.Terminated)
            .ToListAsync(cancellationToken);
        var contractIds = contracts.Select(x => x.Id).ToArray();
        var contractById = contracts.ToDictionary(x => x.Id);
        var linkedBudgetItemIds = contracts
            .Where(x => x.BudgetItemId != null)
            .Select(x => x.BudgetItemId!.Value)
            .ToHashSet();

        var vendorIds = contracts.Select(x => x.VendorId).Distinct().ToArray();
        var vendorNames = vendorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Vendors.AsNoTracking()
                .Where(x => vendorIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var explicitRenewals = contractIds.Length == 0
            ? []
            : await dbContext.ContractRenewals.AsNoTracking()
                .Where(x => contractIds.Contains(x.ContractId)
                    && x.RenewalDate >= fiscalYear.StartDate
                    && x.RenewalDate <= fiscalYear.EndDate)
                .OrderBy(x => x.RenewalDate)
                .ToListAsync(cancellationToken);

        var result = new List<RenewalProjectionItem>();
        var contractsWithExplicitRenewal = explicitRenewals.Select(x => x.ContractId).ToHashSet();
        foreach (var renewal in explicitRenewals)
        {
            var contract = contractById[renewal.ContractId];
            result.Add(new RenewalProjectionItem(
                renewal.Id,
                "Contract renewal",
                contract.BudgetItemId,
                contract.Id,
                contract.ContractNumber,
                contract.Name,
                vendorNames.GetValueOrDefault(contract.VendorId),
                renewal.RenewalDate,
                renewal.NoticeDate,
                renewal.ExpectedAmount,
                renewal.Status.ToString()));
        }

        foreach (var contract in contracts
                     .Where(x => !contractsWithExplicitRenewal.Contains(x.Id)
                         && x.EndDate >= fiscalYear.StartDate
                         && x.EndDate <= fiscalYear.EndDate)
                     .OrderBy(x => x.EndDate))
        {
            result.Add(new RenewalProjectionItem(
                contract.Id,
                "Contract",
                contract.BudgetItemId,
                contract.Id,
                contract.ContractNumber,
                contract.Name,
                vendorNames.GetValueOrDefault(contract.VendorId),
                contract.EndDate,
                contract.RenewalNoticeDate,
                contract.EstimatedAnnualAmount,
                contract.State.ToString()));
        }

        var latestVersionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersionId is not null)
        {
            var budgetFallback = await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.FiscalYearId == fiscalYearId
                    && x.BudgetVersionId == latestVersionId.Value
                    && x.RenewalDate != null
                    && x.RenewalDate >= fiscalYear.StartDate
                    && x.RenewalDate <= fiscalYear.EndDate)
                .OrderBy(x => x.RenewalDate)
                .Select(x => new
                {
                    x.Id,
                    x.ItemNumber,
                    x.Description,
                    x.RenewalDate,
                    x.RevisedTotal,
                    x.ApprovedTotal,
                    x.PlannedTotal,
                    x.Status
                })
                .ToListAsync(cancellationToken);

            result.AddRange(budgetFallback
                .Where(x => !linkedBudgetItemIds.Contains(x.Id))
                .Select(x => new RenewalProjectionItem(
                    x.Id,
                    "Budget fallback",
                    x.Id,
                    null,
                    x.ItemNumber,
                    x.Description,
                    null,
                    x.RenewalDate!.Value,
                    null,
                    x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal,
                    x.Status.ToString())));
        }

        return result
            .OrderBy(x => x.RenewalDate)
            .ThenBy(x => x.Reference)
            .ToArray();
    }
}
