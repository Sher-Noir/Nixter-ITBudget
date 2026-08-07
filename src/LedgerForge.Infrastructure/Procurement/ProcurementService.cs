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
    decimal Total,
    Guid? SupersedesPurchaseOrderId = null,
    int ChangeOrderSequence = 0);

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
    string? Location,
    decimal ReceivedQuantity = 0m,
    decimal RemainingQuantity = 0m);

public sealed record PurchaseReceiptLineSummary(
    int PurchaseOrderLineNumber,
    string Description,
    decimal QuantityReceived);

public sealed record PurchaseReceiptSummary(
    Guid Id,
    string ReceiptNumber,
    DateOnly ReceivedDate,
    string ReceivedBy,
    string? Note,
    IReadOnlyList<PurchaseReceiptLineSummary> Lines);

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
    IReadOnlyList<ProcurementOption> Locations,
    IReadOnlyList<PurchaseReceiptSummary> Receipts,
    PurchaseOrderSummary? SupersededOrder,
    PurchaseOrderSummary? ChangeOrder);

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
            .Select(x => new { x.Id, x.Number, x.FiscalYearId, x.VendorId, x.Description, x.State, x.SupersedesPurchaseOrderId, x.ChangeOrderSequence })
            .ToListAsync(cancellationToken);

        var orders = orderRows.Select(x => new PurchaseOrderSummary(
            x.Id,
            x.Number,
            yearNames.TryGetValue(x.FiscalYearId, out var yearName) ? yearName : "Unknown fiscal year",
            vendorNames.TryGetValue(x.VendorId, out var vendorName) ? vendorName : "Unknown vendor",
            x.Description,
            x.State,
            totals.TryGetValue(x.Id, out var total) ? total : 0m,
            x.SupersedesPurchaseOrderId,
            x.ChangeOrderSequence)).ToArray();

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
        var lineIds = lineRows.Select(x => x.Id).ToArray();

        var receivedByLine = lineIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.Set<PurchaseReceiptLine>().AsNoTracking()
                .Where(x => lineIds.Contains(x.PurchaseOrderLineId))
                .GroupBy(x => x.PurchaseOrderLineId)
                .Select(x => new { Id = x.Key, Quantity = x.Sum(line => line.QuantityReceived) })
                .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);

        var budgetNames = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == order.FiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        var lines = lineRows.Select(x =>
        {
            var received = receivedByLine.GetValueOrDefault(x.Id);
            return new PurchaseOrderLineSummary(
                x.Id,
                x.LineNumber,
                x.Description,
                x.Quantity,
                x.UnitCost,
                x.LineTotal,
                NameFor(x.BudgetItemId, budgetNames),
                NameFor(x.FinanceAccountId, accountNames),
                NameFor(x.DepartmentId, departmentNames),
                NameFor(x.LocationId, locationNames),
                received,
                Math.Max(0m, x.Quantity - received));
        }).ToArray();

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

        var receipts = await GetReceiptSummariesAsync(id, lineRows, cancellationToken);
        var summary = ToSummary(order, year.DisplayName, vendor.Name, lines.Sum(x => x.LineTotal));

        PurchaseOrderSummary? supersededOrder = null;
        if (order.SupersedesPurchaseOrderId is Guid sourceId)
        {
            var source = await dbContext.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
            if (source is not null)
                supersededOrder = ToSummary(source, year.DisplayName, vendor.Name, await TotalAsync(source.Id, cancellationToken));
        }

        PurchaseOrderSummary? changeOrder = null;
        var child = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(x => x.SupersedesPurchaseOrderId == order.Id)
            .OrderByDescending(x => x.ChangeOrderSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (child is not null)
            changeOrder = ToSummary(child, year.DisplayName, vendor.Name, await TotalAsync(child.Id, cancellationToken));

        return new(summary, lines, budgetItems, financeAccounts, departments, locations, receipts, supersededOrder, changeOrder);
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

    public async Task<Guid> CreateChangeOrderAsync(
        Guid sourcePurchaseOrderId,
        string newNumber,
        string description,
        CancellationToken cancellationToken = default)
    {
        var source = await RequireOrderAsync(sourcePurchaseOrderId, cancellationToken);
        if (source.State != PurchaseOrderState.Issued)
            throw new InvalidOperationException("A change order can only be created from an issued purchase order.");
        if (await dbContext.PurchaseOrders.AnyAsync(
                x => x.SupersedesPurchaseOrderId == source.Id && x.State != PurchaseOrderState.Cancelled && x.State != PurchaseOrderState.Rejected,
                cancellationToken))
            throw new InvalidOperationException("This purchase order already has an active change order. Continue from that revision instead.");

        newNumber = newNumber?.Trim() ?? string.Empty;
        if (await dbContext.PurchaseOrders.AnyAsync(x => x.FiscalYearId == source.FiscalYearId && x.Number == newNumber, cancellationToken))
            throw new InvalidOperationException($"Purchase order number '{newNumber}' already exists in the selected fiscal year.");

        var sourceLines = await dbContext.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == source.Id)
            .OrderBy(x => x.LineNumber)
            .ToListAsync(cancellationToken);
        if (sourceLines.Count == 0) throw new InvalidOperationException("Issued purchase order has no lines to revise.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var changeOrder = new PurchaseOrder(
            source.FiscalYearId,
            source.VendorId,
            newNumber,
            description,
            source.Id,
            source.ChangeOrderSequence + 1);
        dbContext.PurchaseOrders.Add(changeOrder);
        foreach (var line in sourceLines)
        {
            dbContext.PurchaseOrderLines.Add(new PurchaseOrderLine(
                changeOrder.Id,
                line.LineNumber,
                line.Description,
                line.Quantity,
                line.UnitCost,
                line.BudgetItemId,
                line.FinanceAccountId,
                line.DepartmentId,
                line.LocationId));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changeOrder.Id;
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

    public async Task RecordReceiptAsync(
        Guid purchaseOrderId,
        string receiptNumber,
        DateOnly receivedDate,
        string actor,
        string? note,
        IReadOnlyDictionary<Guid, decimal> quantities,
        CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(purchaseOrderId, cancellationToken);
        if (order.State != PurchaseOrderState.Issued)
            throw new InvalidOperationException("Receipts can only be posted against an issued purchase order.");
        if (receivedDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("Receipt date cannot be in the future.");
        if (quantities.Count == 0 || quantities.All(x => x.Value <= 0m))
            throw new InvalidOperationException("Enter a positive received quantity for at least one purchase order line.");
        if (await dbContext.Set<PurchaseReceipt>().AnyAsync(x => x.PurchaseOrderId == purchaseOrderId && x.ReceiptNumber == receiptNumber.Trim(), cancellationToken))
            throw new InvalidOperationException($"Receipt number '{receiptNumber.Trim()}' already exists for this purchase order.");

        var lines = await dbContext.PurchaseOrderLines.Where(x => x.PurchaseOrderId == purchaseOrderId).ToListAsync(cancellationToken);
        var lineById = lines.ToDictionary(x => x.Id);
        var selectedLineIds = quantities.Where(x => x.Value > 0m).Select(x => x.Key).ToArray();
        if (selectedLineIds.Any(id => !lineById.ContainsKey(id)))
            throw new InvalidOperationException("One or more receipt lines do not belong to this purchase order.");

        var existingReceived = selectedLineIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.Set<PurchaseReceiptLine>().AsNoTracking()
                .Where(x => selectedLineIds.Contains(x.PurchaseOrderLineId))
                .GroupBy(x => x.PurchaseOrderLineId)
                .Select(x => new { Id = x.Key, Quantity = x.Sum(line => line.QuantityReceived) })
                .ToDictionaryAsync(x => x.Id, x => x.Quantity, cancellationToken);

        foreach (var pair in quantities.Where(x => x.Value > 0m))
        {
            var ordered = lineById[pair.Key].Quantity;
            var previouslyReceived = existingReceived.GetValueOrDefault(pair.Key);
            if (pair.Value + previouslyReceived > ordered)
                throw new InvalidOperationException($"Line {lineById[pair.Key].LineNumber} would exceed ordered quantity {ordered:N4}. Remaining receivable quantity is {Math.Max(0m, ordered - previouslyReceived):N4}.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var receipt = new PurchaseReceipt(purchaseOrderId, receiptNumber, receivedDate, actor, note);
        dbContext.Set<PurchaseReceipt>().Add(receipt);
        foreach (var pair in quantities.Where(x => x.Value > 0m))
            dbContext.Set<PurchaseReceiptLine>().Add(new PurchaseReceiptLine(receipt.Id, pair.Key, pair.Value));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequireOrderAsync(id, cancellationToken);
        var total = await TotalAsync(id, cancellationToken);
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        order.Issue(actor, now);
        if (order.SupersedesPurchaseOrderId is Guid sourceId)
        {
            var source = await dbContext.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken)
                ?? throw new InvalidOperationException("The purchase order revision source no longer exists.");
            if (source.State != PurchaseOrderState.Issued)
                throw new InvalidOperationException($"The superseded purchase order must still be Issued when the change order is issued; current state is {source.State}.");
            source.Close(actor, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private async Task<IReadOnlyList<PurchaseReceiptSummary>> GetReceiptSummariesAsync(
        Guid purchaseOrderId,
        IReadOnlyList<PurchaseOrderLine> orderLines,
        CancellationToken cancellationToken)
    {
        var receipts = await dbContext.Set<PurchaseReceipt>().AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(x => x.ReceivedDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (receipts.Count == 0) return [];

        var receiptIds = receipts.Select(x => x.Id).ToArray();
        var receiptLines = await dbContext.Set<PurchaseReceiptLine>().AsNoTracking()
            .Where(x => receiptIds.Contains(x.PurchaseReceiptId))
            .ToListAsync(cancellationToken);
        var poLines = orderLines.ToDictionary(x => x.Id);
        return receipts.Select(receipt => new PurchaseReceiptSummary(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.ReceivedDate,
            receipt.ReceivedBy,
            receipt.Note,
            receiptLines.Where(x => x.PurchaseReceiptId == receipt.Id)
                .Select(x => poLines.TryGetValue(x.PurchaseOrderLineId, out var poLine)
                    ? new PurchaseReceiptLineSummary(poLine.LineNumber, poLine.Description, x.QuantityReceived)
                    : new PurchaseReceiptLineSummary(0, "Removed purchase-order line", x.QuantityReceived))
                .OrderBy(x => x.PurchaseOrderLineNumber)
                .ToArray())).ToArray();
    }

    private async Task<decimal> TotalAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
        => await dbContext.PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .SumAsync(x => (decimal?)x.LineTotal, cancellationToken) ?? 0m;

    private static PurchaseOrderSummary ToSummary(PurchaseOrder order, string fiscalYear, string vendor, decimal total)
        => new(order.Id, order.Number, fiscalYear, vendor, order.Description, order.State, total, order.SupersedesPurchaseOrderId, order.ChangeOrderSequence);

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
