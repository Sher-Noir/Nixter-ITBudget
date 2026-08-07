using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Procurement;

public sealed record ProcurementOption(Guid Id, string Label);

public sealed record VendorSummary(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    bool IsActive);

public sealed record PurchaseOrderSummary(
    Guid Id,
    string Number,
    string FiscalYear,
    string Vendor,
    string Description,
    PurchaseOrderState State,
    decimal Total);

public sealed record PurchaseOrderLineSummary(
    Guid Id,
    int LineNumber,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal,
    string? BudgetItem,
    string? FinanceAccount,
    string? Department,
    string? Location);

public sealed record PurchaseOrderIndexSnapshot(
    IReadOnlyList<PurchaseOrderSummary> Orders,
    IReadOnlyList<ProcurementOption> FiscalYears,
    IReadOnlyList<ProcurementOption> Vendors);

public sealed record PurchaseOrderDetailSnapshot(
    PurchaseOrderSummary Order,
    IReadOnlyList<PurchaseOrderLineSummary> Lines,
    IReadOnlyList<ProcurementOption> BudgetItems,
    IReadOnlyList<ProcurementOption> FinanceAccounts,
    IReadOnlyList<ProcurementOption> Departments,
    IReadOnlyList<ProcurementOption> Locations);

public sealed class ProcurementService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<VendorSummary>> ListVendorsAsync(CancellationToken cancellationToken = default)
        => await dbContext.Vendors
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new VendorSummary(x.Id, x.Code, x.Name, x.ContactName, x.Email, x.Phone, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task AddVendorAsync(
        string code,
        string name,
        string? contactName,
        string? email,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        code = code?.Trim() ?? string.Empty;
        if (await dbContext.Vendors.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException($"Vendor code '{code}' already exists.");

        var vendor = new Vendor(code, name);
        vendor.Update(name, contactName, email, phone);
        dbContext.Vendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateVendorAsync(
        Guid id,
        string name,
        string? contactName,
        string? email,
        string? phone,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var vendor = await dbContext.Vendors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Vendor was not found.");
        vendor.Update(name, contactName, email, phone);
        if (isActive) vendor.Activate();
        else vendor.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PurchaseOrderIndexSnapshot> GetPurchaseOrdersAsync(CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new ProcurementOption(x.Id, x.DisplayName))
            .ToListAsync(cancellationToken);
        var vendors = await dbContext.Vendors.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ProcurementOption(x.Id, x.Code + " · " + x.Name))
            .ToListAsync(cancellationToken);

        var yearNames = await dbContext.FiscalYears.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var vendorNames = await dbContext.Vendors.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var totals = await dbContext.PurchaseOrderLines.AsNoTracking()
            .GroupBy(x => x.PurchaseOrderId)
            .Select(x => new { PurchaseOrderId = x.Key, Total = x.Sum(line => line.LineTotal) })
            .ToDictionaryAsync(x => x.PurchaseOrderId, x => x.Total, cancellationToken);

        var orderRows = await dbContext.PurchaseOrders.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Number)
            .Select(x => new { x.Id, x.Number, x.FiscalYearId, x.VendorId, x.Description, x.State })
            .ToListAsync(cancellationToken);

        var orders = orderRows.Select(x => new PurchaseOrderSummary(
            x.Id,
            x.Number,
            yearNames.TryGetValue(x.FiscalYearId, out var yearName) ? yearName : "Unknown fiscal year",
            vendorNames.TryGetValue(x.VendorId, out var vendorName) ? vendorName : "Unknown vendor",
            x.Description,
            x.State,
            totals.TryGetValue(x.Id, out var total) ? total : 0m)).ToArray();

        return new(orders, years, vendors);
    }

    public async Task<PurchaseOrderDetailSnapshot?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return null;

        var year = await dbContext.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == order.FiscalYearId, cancellationToken);
        var vendor = await dbContext.Vendors.AsNoTracking().SingleAsync(x => x.Id == order.VendorId, cancellationToken);
        var lineRows = await dbContext.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == id)
            .OrderBy(x => x.LineNumber)
            .ToListAsync(cancellationToken);

        var budgetNames = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == order.FiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        var lines = lineRows.Select(x => new PurchaseOrderLineSummary(
            x.Id,
            x.LineNumber,
            x.Description,
            x.Quantity,
            x.UnitCost,
            x.LineTotal,
            NameFor(x.BudgetItemId, budgetNames),
            NameFor(x.FinanceAccountId, accountNames),
            NameFor(x.DepartmentId, departmentNames),
            NameFor(x.LocationId, locationNames))).ToArray();

        var latestVersionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == order.FiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        IReadOnlyList<ProcurementOption> budgetItems = latestVersionId is null
            ? []
            : await dbContext.BudgetItems.AsNoTracking()
                .Where(x => x.FiscalYearId == order.FiscalYearId &&
                            x.BudgetVersionId == latestVersionId.Value &&
                            x.Status != BudgetItemStatus.Cancelled &&
                            x.Status != BudgetItemStatus.Archived)
                .OrderBy(x => x.ItemNumber)
                .Select(x => new ProcurementOption(x.Id, x.ItemNumber + " · " + x.Description))
                .ToListAsync(cancellationToken);

        var financeAccounts = await ActiveOptionsAsync(dbContext.FinanceAccounts, cancellationToken);
        var departments = await ActiveOptionsAsync(dbContext.Departments, cancellationToken);
        var locations = await ActiveOptionsAsync(dbContext.Locations, cancellationToken);

        var summary = new PurchaseOrderSummary(order.Id, order.Number, year.DisplayName, vendor.Name, order.Description, order.State, lines.Sum(x => x.LineTotal));
        return new(summary, lines, budgetItems, financeAccounts, departments, locations);
    }

    public async Task<Guid> CreatePurchaseOrderAsync(
        Guid fiscalYearId,
        Guid vendorId,
        string number,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.FiscalYears.AnyAsync(x => x.Id == fiscalYearId, cancellationToken))
            throw new ArgumentException("Selected fiscal year does not exist.", nameof(fiscalYearId));
        if (!await dbContext.Vendors.AnyAsync(x => x.Id == vendorId && x.IsActive, cancellationToken))
            throw new ArgumentException("Selected vendor does not exist or is inactive.", nameof(vendorId));

        number = number?.Trim() ?? string.Empty;
        if (await dbContext.PurchaseOrders.AnyAsync(x => x.FiscalYearId == fiscalYearId && x.Number == number, cancellationToken))
            throw new InvalidOperationException($"Purchase order number '{number}' already exists in the selected fiscal year.");

        var order = new PurchaseOrder(fiscalYearId, vendorId, number, description);
        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return order.Id;
    }

    public async Task AddLineAsync(
        Guid purchaseOrderId,
        string description,
        decimal quantity,
        decimal unitCost,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(purchaseOrderId, cancellationToken);
        if (order.State != PurchaseOrderState.Draft)
            throw new InvalidOperationException("Lines can only be added to draft purchase orders.");

        if (budgetItemId is not null)
        {
            if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item ID cannot be empty.", nameof(budgetItemId));
            if (!await dbContext.BudgetItems.AnyAsync(x => x.Id == budgetItemId && x.FiscalYearId == order.FiscalYearId, cancellationToken))
                throw new ArgumentException("Selected budget item does not belong to the purchase order fiscal year.", nameof(budgetItemId));
        }

        await RequireActiveLookupAsync(dbContext.FinanceAccounts, financeAccountId, "finance account", cancellationToken);
        await RequireActiveLookupAsync(dbContext.Departments, departmentId, "department", cancellationToken);
        await RequireActiveLookupAsync(dbContext.Locations, locationId, "location", cancellationToken);

        var lastLine = await dbContext.PurchaseOrderLines
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .MaxAsync(x => (int?)x.LineNumber, cancellationToken) ?? 0;

        var line = new PurchaseOrderLine(
            purchaseOrderId,
            lastLine + 1,
            description,
            quantity,
            unitCost,
            budgetItemId,
            financeAccountId,
            departmentId,
            locationId);
        dbContext.PurchaseOrderLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        var total = await dbContext.PurchaseOrderLines.Where(x => x.PurchaseOrderId == id).SumAsync(x => (decimal?)x.LineTotal, cancellationToken) ?? 0m;
        if (total <= 0m) throw new InvalidOperationException("A purchase order must contain at least one positive-value line before submission.");
        order.Submit(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        order.Approve(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IssueAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        order.Issue(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        order.Close(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        order.Cancel(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PurchaseOrder> RequireOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Purchase order ID is required.", nameof(id));
        return await dbContext.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order was not found.");
    }

    private static async Task<IReadOnlyList<ProcurementOption>> ActiveOptionsAsync<TEntity>(
        DbSet<TEntity> set,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
        => await set.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new ProcurementOption(x.Id, x.Code + " · " + x.Name))
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

    private static string? NameFor(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is Guid value && names.TryGetValue(value, out var name) ? name : null;
}
