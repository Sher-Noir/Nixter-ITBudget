using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Approvals;

public enum ApprovalQueueItemType
{
    BudgetItem,
    PurchaseOrder
}

public sealed record ApprovalQueueItem(
    ApprovalQueueItemType Type,
    Guid Id,
    string Reference,
    string Description,
    string FiscalYear,
    decimal Amount,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAtUtc);

public sealed record ApprovalQueueSnapshot(
    IReadOnlyList<ApprovalQueueItem> Items,
    int BudgetItemCount,
    int PurchaseOrderCount,
    decimal PendingAmount);

public sealed class ApprovalQueueService(LedgerForgeDbContext dbContext)
{
    public async Task<ApprovalQueueSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var fiscalYearNames = await dbContext.FiscalYears
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var budgetRows = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.Status == BudgetItemStatus.Submitted)
            .OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FiscalYearId,
                x.ItemNumber,
                x.Description,
                x.PlannedTotal,
                x.RevisedTotal,
                x.SubmittedBy,
                x.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);

        var poTotals = await dbContext.PurchaseOrderLines
            .AsNoTracking()
            .GroupBy(x => x.PurchaseOrderId)
            .Select(x => new { Id = x.Key, Total = x.Sum(line => line.LineTotal) })
            .ToDictionaryAsync(x => x.Id, x => x.Total, cancellationToken);

        var poRows = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.State == PurchaseOrderState.PendingApproval)
            .OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FiscalYearId,
                x.Number,
                x.Description,
                x.SubmittedBy,
                x.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);

        var items = new List<ApprovalQueueItem>(budgetRows.Count + poRows.Count);
        items.AddRange(budgetRows.Select(x => new ApprovalQueueItem(
            ApprovalQueueItemType.BudgetItem,
            x.Id,
            x.ItemNumber,
            x.Description,
            FiscalYearName(x.FiscalYearId, fiscalYearNames),
            x.RevisedTotal ?? x.PlannedTotal,
            x.SubmittedBy,
            x.SubmittedAtUtc)));
        items.AddRange(poRows.Select(x => new ApprovalQueueItem(
            ApprovalQueueItemType.PurchaseOrder,
            x.Id,
            x.Number,
            x.Description,
            FiscalYearName(x.FiscalYearId, fiscalYearNames),
            poTotals.TryGetValue(x.Id, out var total) ? total : 0m,
            x.SubmittedBy,
            x.SubmittedAtUtc)));

        var ordered = items
            .OrderBy(x => x.SubmittedAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Type)
            .ThenBy(x => x.Reference)
            .ToArray();

        return new(
            ordered,
            budgetRows.Count,
            poRows.Count,
            ordered.Sum(x => x.Amount));
    }

    public async Task SubmitBudgetItemAsync(
        Guid itemId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.BudgetItems
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");

        var version = await dbContext.BudgetVersions
            .AsNoTracking()
            .SingleAsync(x => x.Id == item.BudgetVersionId, cancellationToken);
        if (version.IsLocked)
            throw new InvalidOperationException("Items in a locked budget version cannot be submitted.");

        item.Submit(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveBudgetItemAsync(
        Guid itemId,
        string actor,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Approve(actor, DateTimeOffset.UtcNow, note);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DenyBudgetItemAsync(
        Guid itemId,
        string actor,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Deny(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeferBudgetItemAsync(
        Guid itemId,
        string actor,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Defer(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApprovePurchaseOrderAsync(
        Guid purchaseOrderId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var order = await RequirePurchaseOrderAsync(purchaseOrderId, cancellationToken);
        order.Approve(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectPurchaseOrderAsync(
        Guid purchaseOrderId,
        string actor,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var order = await RequirePurchaseOrderAsync(purchaseOrderId, cancellationToken);
        order.Reject(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<BudgetItem> RequireBudgetItemAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Budget item ID is required.", nameof(id));
        return await dbContext.BudgetItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");
    }

    private async Task<PurchaseOrder> RequirePurchaseOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Purchase order ID is required.", nameof(id));
        return await dbContext.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order was not found.");
    }

    private static string FiscalYearName(Guid fiscalYearId, IReadOnlyDictionary<Guid, string> names)
        => names.TryGetValue(fiscalYearId, out var name) ? name : "Unknown fiscal year";
}
