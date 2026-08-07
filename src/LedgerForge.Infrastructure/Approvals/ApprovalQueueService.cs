using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Approvals;

public enum ApprovalQueueItemType
{
    BudgetItem,
    PurchaseOrder,
    Invoice,
    BudgetAmendment
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
    int InvoiceCount,
    int BudgetAmendmentCount,
    decimal PendingAmount)
{
    public int TotalCount => Items.Count;
}

public sealed class ApprovalQueueService(
    LedgerForgeDbContext dbContext,
    BudgetAmendmentService budgetAmendmentService,
    InvoiceWorkflowService invoiceWorkflowService)
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

        var invoiceRows = await dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.State == InvoiceState.PendingApproval)
            .OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FiscalYearId,
                x.InvoiceNumber,
                x.Description,
                x.TotalAmount,
                x.SubmittedBy,
                x.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);

        var amendmentRows = await dbContext.BudgetAmendments
            .AsNoTracking()
            .Where(x => x.State == BudgetAmendmentState.PendingApproval)
            .OrderBy(x => x.SubmittedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.FiscalYearId,
                x.BudgetItemId,
                x.AmountDelta,
                x.Reason,
                x.SubmittedBy,
                x.SubmittedAtUtc
            })
            .ToListAsync(cancellationToken);
        var amendmentBudgetItemIds = amendmentRows.Select(x => x.BudgetItemId).Distinct().ToArray();
        var amendmentBudgetItems = amendmentBudgetItemIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.BudgetItems.AsNoTracking()
                .Where(x => amendmentBudgetItemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);

        var items = new List<ApprovalQueueItem>(budgetRows.Count + poRows.Count + invoiceRows.Count + amendmentRows.Count);
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
        items.AddRange(invoiceRows.Select(x => new ApprovalQueueItem(
            ApprovalQueueItemType.Invoice,
            x.Id,
            x.InvoiceNumber,
            x.Description,
            FiscalYearName(x.FiscalYearId, fiscalYearNames),
            x.TotalAmount,
            x.SubmittedBy,
            x.SubmittedAtUtc)));
        items.AddRange(amendmentRows.Select(x => new ApprovalQueueItem(
            ApprovalQueueItemType.BudgetAmendment,
            x.Id,
            amendmentBudgetItems.GetValueOrDefault(x.BudgetItemId) ?? "Budget amendment",
            x.Reason,
            FiscalYearName(x.FiscalYearId, fiscalYearNames),
            x.AmountDelta,
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
            invoiceRows.Count,
            amendmentRows.Count,
            ordered.Sum(x => Math.Abs(x.Amount)));
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

    public async Task ApproveBudgetItemAsync(Guid itemId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Approve(actor, DateTimeOffset.UtcNow, note);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DenyBudgetItemAsync(Guid itemId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Deny(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeferBudgetItemAsync(Guid itemId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        item.Defer(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApprovePurchaseOrderAsync(Guid purchaseOrderId, string actor, CancellationToken cancellationToken = default)
    {
        var order = await RequirePurchaseOrderAsync(purchaseOrderId, cancellationToken);
        order.Approve(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectPurchaseOrderAsync(Guid purchaseOrderId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var order = await RequirePurchaseOrderAsync(purchaseOrderId, cancellationToken);
        order.Reject(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ApproveInvoiceAsync(Guid invoiceId, string actor, CancellationToken cancellationToken = default)
        => invoiceWorkflowService.ApproveAsync(invoiceId, actor, cancellationToken);

    public Task RejectInvoiceAsync(Guid invoiceId, string actor, string reason, CancellationToken cancellationToken = default)
        => invoiceWorkflowService.RejectAsync(invoiceId, actor, reason, cancellationToken);

    public Task ApproveBudgetAmendmentAsync(Guid amendmentId, string actor, string? note, CancellationToken cancellationToken = default)
        => budgetAmendmentService.ApproveAsync(amendmentId, actor, note, cancellationToken);

    public Task RejectBudgetAmendmentAsync(Guid amendmentId, string actor, string reason, CancellationToken cancellationToken = default)
        => budgetAmendmentService.RejectAsync(amendmentId, actor, reason, cancellationToken);

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
