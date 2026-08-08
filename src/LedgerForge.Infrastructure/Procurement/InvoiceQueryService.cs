using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Procurement;

public sealed record InvoiceOption(Guid Id, string Label);

public sealed record InvoiceSummary(
    Guid Id,
    string FiscalYear,
    string Vendor,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string Description,
    decimal TotalAmount,
    decimal AllocatedAmount,
    InvoiceState State,
    string? PurchaseOrderNumber);

public sealed record InvoiceAllocationSummary(
    Guid Id,
    int LineNumber,
    string Description,
    decimal Amount,
    string? BudgetItem,
    string? FinanceAccount,
    string? Department,
    string? Location,
    string? FiscalPeriod);

public sealed record InvoiceIndexSnapshot(
    IReadOnlyList<InvoiceSummary> Invoices,
    IReadOnlyList<InvoiceOption> FiscalYears,
    IReadOnlyList<InvoiceOption> Vendors,
    IReadOnlyList<InvoiceOption> PurchaseOrders);

public sealed record InvoiceDetailSnapshot(
    InvoiceSummary Invoice,
    IReadOnlyList<InvoiceAllocationSummary> Allocations,
    IReadOnlyList<InvoiceOption> BudgetItems,
    IReadOnlyList<InvoiceOption> FinanceAccounts,
    IReadOnlyList<InvoiceOption> Departments,
    IReadOnlyList<InvoiceOption> Locations,
    IReadOnlyList<InvoiceOption> FiscalPeriods,
    bool HasPostedActuals);

public sealed class InvoiceQueryService(LedgerForgeDbContext dbContext)
{
    public async Task<InvoiceIndexSnapshot> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
        var vendors = await dbContext.Vendors.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var yearNames = years.ToDictionary(x => x.Id, x => x.DisplayName);
        var vendorNames = vendors.ToDictionary(x => x.Id, x => x.Name);

        var poRows = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(x => x.State == PurchaseOrderState.Issued || x.State == PurchaseOrderState.Closed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var poNumbers = poRows.ToDictionary(x => x.Id, x => x.Number);
        var poOptions = poRows.Select(x => new InvoiceOption(
            x.Id,
            x.Number + " · " + Name(x.VendorId, vendorNames, "Unknown vendor") + " · " + Name(x.FiscalYearId, yearNames, "Unknown fiscal year")))
            .ToArray();

        var allocationTotals = await dbContext.InvoiceAllocations.AsNoTracking()
            .GroupBy(x => x.InvoiceId)
            .Select(x => new { Id = x.Key, Total = x.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, cancellationToken);
        var invoiceRows = await dbContext.Invoices.AsNoTracking()
            .OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var invoices = invoiceRows.Select(x => new InvoiceSummary(
            x.Id,
            Name(x.FiscalYearId, yearNames, "Unknown fiscal year"),
            Name(x.VendorId, vendorNames, "Unknown vendor"),
            x.InvoiceNumber,
            x.InvoiceDate,
            x.Description,
            x.TotalAmount,
            allocationTotals.TryGetValue(x.Id, out var allocated) ? allocated : 0m,
            x.State,
            x.PurchaseOrderId is Guid poId && poNumbers.TryGetValue(poId, out var po) ? po : null)).ToArray();

        return new(
            invoices,
            years.Select(x => new InvoiceOption(x.Id, x.DisplayName)).ToArray(),
            vendors.Where(x => x.IsActive).Select(x => new InvoiceOption(x.Id, x.Code + " · " + x.Name)).ToArray(),
            poOptions);
    }

    public async Task<InvoiceDetailSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null) return null;

        var year = await dbContext.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == invoice.FiscalYearId, cancellationToken);
        var vendor = await dbContext.Vendors.AsNoTracking().SingleAsync(x => x.Id == invoice.VendorId, cancellationToken);
        var poNumber = invoice.PurchaseOrderId is Guid poId
            ? await dbContext.PurchaseOrders.AsNoTracking().Where(x => x.Id == poId).Select(x => x.Number).SingleOrDefaultAsync(cancellationToken)
            : null;
        var allocations = await dbContext.InvoiceAllocations.AsNoTracking()
            .Where(x => x.InvoiceId == id).OrderBy(x => x.LineNumber).ToListAsync(cancellationToken);

        var budgetNames = await dbContext.BudgetItems.AsNoTracking().Where(x => x.FiscalYearId == invoice.FiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var periodNames = await dbContext.FiscalPeriods.AsNoTracking().Where(x => x.FiscalYearId == invoice.FiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        var allocationSummaries = allocations.Select(x => new InvoiceAllocationSummary(
            x.Id, x.LineNumber, x.Description, x.Amount,
            Name(x.BudgetItemId, budgetNames), Name(x.FinanceAccountId, accountNames),
            Name(x.DepartmentId, departmentNames), Name(x.LocationId, locationNames), Name(x.FiscalPeriodId, periodNames))).ToArray();

        var latestVersionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == invoice.FiscalYearId).OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        IReadOnlyList<InvoiceOption> budgetOptions = latestVersionId is null
            ? []
            : await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.FiscalYearId == invoice.FiscalYearId && x.BudgetVersionId == latestVersionId.Value &&
                            x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived)
                .OrderBy(x => x.ItemNumber)
                .Select(x => new InvoiceOption(x.Id, x.ItemNumber + " · " + x.Description))
                .ToListAsync(cancellationToken);

        var financeOptions = await ActiveOptions(dbContext.FinanceAccounts, cancellationToken);
        var departmentOptions = await ActiveOptions(dbContext.Departments, cancellationToken);
        var locationOptions = await ActiveOptions(dbContext.Locations, cancellationToken);
        var periodOptions = await dbContext.FiscalPeriods.AsNoTracking()
            .Where(x => x.FiscalYearId == invoice.FiscalYearId).OrderBy(x => x.PeriodNumber)
            .Select(x => new InvoiceOption(x.Id, x.Code + " · " + x.Name + (x.IsClosed ? " (closed)" : string.Empty)))
            .ToListAsync(cancellationToken);

        var summary = new InvoiceSummary(
            invoice.Id, year.DisplayName, vendor.Name, invoice.InvoiceNumber, invoice.InvoiceDate,
            invoice.Description, invoice.TotalAmount, allocationSummaries.Sum(x => x.Amount), invoice.State, poNumber);
        var hasActuals = await dbContext.ActualTransactions.AsNoTracking().AnyAsync(x => x.InvoiceId == id, cancellationToken);
        return new(summary, allocationSummaries, budgetOptions, financeOptions, departmentOptions, locationOptions, periodOptions, hasActuals);
    }

    private static async Task<IReadOnlyList<InvoiceOption>> ActiveOptions<TEntity>(DbSet<TEntity> set, CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
        => await set.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new InvoiceOption(x.Id, x.Code + " · " + x.Name)).ToListAsync(cancellationToken);

    private static string Name(Guid id, IReadOnlyDictionary<Guid, string> names, string fallback)
        => names.TryGetValue(id, out var name) ? name : fallback;

    private static string? Name(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is Guid value && names.TryGetValue(value, out var name) ? name : null;
}
