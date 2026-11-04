using LedgerForge.Domain.Auditing;
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
    public Task<ApprovalQueueSnapshot> GetAsync(CancellationToken cancellationToken = default)
        => GetAsync(null, cancellationToken);

    public async Task<ApprovalQueueSnapshot> GetAsync(Guid? fiscalYearId, CancellationToken cancellationToken = default)
    {
        var fiscalYearNames = await dbContext.FiscalYears
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var budgetRows = await dbContext.BudgetItems
            .AsNoTracking()
            .Where(x => x.Status == BudgetItemStatus.Submitted && (fiscalYearId == null || x.FiscalYearId == fiscalYearId.Value))
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
            .Where(x => x.State == PurchaseOrderState.PendingApproval && (fiscalYearId == null || x.FiscalYearId == fiscalYearId.Value))
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
            .Where(x => x.State == InvoiceState.PendingApproval && (fiscalYearId == null || x.FiscalYearId == fiscalYearId.Value))
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
            .Where(x => x.State == BudgetAmendmentState.PendingApproval && (fiscalYearId == null || x.FiscalYearId == fiscalYearId.Value))
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

    // A batch is all-or-nothing: one invalid item stops the entire selection.
    // Each modified BudgetItem is covered by the standard SaveChanges audit interceptor.
    public async Task<int> SubmitBudgetItemsAsync(
        Guid fiscalYearId, Guid versionId, IReadOnlyCollection<Guid> itemIds,
        string actor, CancellationToken cancellationToken = default)
    {
        var ids = ValidateBulkSelection(itemIds, actor);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == versionId && x.FiscalYearId == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Selected budget version was not found.");
        var year = await dbContext.FiscalYears.AsNoTracking()
            .SingleAsync(x => x.Id == fiscalYearId, cancellationToken);
        if (version.IsLocked || year.LockedAtUtc is not null ||
            year.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived)
            throw new InvalidOperationException("Only an open, unlocked budget version may be submitted.");

        var items = await dbContext.BudgetItems
            .Where(x => ids.Contains(x.Id) && x.FiscalYearId == fiscalYearId &&
                        x.BudgetVersionId == versionId)
            .ToListAsync(cancellationToken);
        if (items.Count != ids.Length)
            throw new InvalidOperationException("Selected items must all belong to the chosen fiscal year and version.");
        if (items.Any(x => x.IsArchived || !x.IsPlanningEditable))
            throw new InvalidOperationException("All selected items must be editable Draft, Proposed or Deferred items.");

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items) item.Submit(actor, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return items.Count;
    }

    public async Task<int> ApproveBudgetItemsAsync(
        IReadOnlyCollection<Guid> itemIds, string actor, string? note,
        CancellationToken cancellationToken = default)
    {
        var ids = ValidateBulkSelection(itemIds, actor);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var items = await dbContext.BudgetItems.Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (items.Count != ids.Length || items.Any(x => x.IsArchived || x.Status != BudgetItemStatus.Submitted))
            throw new InvalidOperationException("All selected budget items must still be submitted and pending approval.");

        var versionIds = items.Select(x => x.BudgetVersionId).Distinct().ToArray();
        var versions = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var yearIds = items.Select(x => x.FiscalYearId).Distinct().ToArray();
        var years = await dbContext.FiscalYears.AsNoTracking()
            .Where(x => yearIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (versions.Count != versionIds.Length || versions.Any(x => x.IsLocked) ||
            years.Count != yearIds.Length ||
            years.Any(x => x.LockedAtUtc is not null ||
                x.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived))
            throw new InvalidOperationException("An item belongs to a locked or closed budget; nothing was approved.");

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items) item.Approve(actor, now, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return items.Count;
    }

    private static Guid[] ValidateBulkSelection(IReadOnlyCollection<Guid> itemIds, string actor)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("An authenticated actor is required.", nameof(actor));
        if (itemIds is null || itemIds.Count == 0)
            throw new ArgumentException("Select at least one budget item.", nameof(itemIds));
        if (itemIds.Count > 200 || itemIds.Contains(Guid.Empty) || itemIds.Distinct().Count() != itemIds.Count)
            throw new ArgumentException("Choose 1 to 200 distinct, valid items per batch.", nameof(itemIds));
        return itemIds.ToArray();
    }

    public async Task SubmitBudgetItemAsync(Guid itemId, string actor, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.BudgetItems.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");
        var version = await dbContext.BudgetVersions.AsNoTracking().SingleAsync(x => x.Id == item.BudgetVersionId, cancellationToken);
        if (version.IsLocked) throw new InvalidOperationException("Items in a locked budget version cannot be submitted.");
        item.Submit(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenBudgetItemForRevisionAsync(
        Guid itemId, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("An authenticated actor is required.", nameof(actor));
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .SingleAsync(x => x.Id == item.BudgetVersionId, cancellationToken);
        if (version.IsLocked)
            throw new InvalidOperationException("This budget version is locked and cannot be revised.");

        var previousStatus = item.Status;
        item.ReopenForRevision();
        dbContext.AuditEvents.Add(new AuditEvent(
            actor,
            nameof(BudgetItem),
            item.Id,
            AuditAction.Explicit,
            System.Text.Json.JsonSerializer.Serialize(new { Status = previousStatus.ToString(), item.ApprovedTotal }),
            System.Text.Json.JsonSerializer.Serialize(new { Status = item.Status.ToString(), item.ApprovedTotal, RevisionRequiresApproval = true }),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetBudgetItemPurchasedAsync(
        Guid itemId, string actor, bool purchased, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("An authenticated actor is required.", nameof(actor));
        var item = await RequireBudgetItemAsync(itemId, cancellationToken);
        var fiscalYear = await dbContext.FiscalYears.AsNoTracking()
            .SingleAsync(x => x.Id == item.FiscalYearId, cancellationToken);
        if (fiscalYear.Status is FiscalYearStatus.Closed or FiscalYearStatus.Archived ||
            fiscalYear.LockedAtUtc is not null)
            throw new InvalidOperationException("A closed, archived, or locked fiscal year cannot change purchase status.");

        // Purchase status is operational tracking, not an alteration to the
        // budget version's financial approval/baseline or the actual ledger.
        // Purchases must still be recordable after a budget version is locked.
        var previousStatus = item.Status;
        if (purchased)
        {
            item.MarkPurchased();
        }
        else
        {
            // The last explicit Purchased event includes the original status;
            // restore it exactly so an Active item does not become Draft by
            // accident when the purchase flag is undone.
            var previousStatusJson = await dbContext.AuditEvents.AsNoTracking()
                .Where(x => x.EntityType == nameof(BudgetItem) && x.EntityId == item.Id &&
                    x.Action == AuditAction.Explicit && x.AfterJson != null &&
                    x.AfterJson.Contains("\"TrackingOnly\":true") &&
                    x.AfterJson.Contains("\"Status\":\"Purchased\""))
                .OrderByDescending(x => x.OccurredAtUtc)
                .Select(x => x.BeforeJson)
                .FirstOrDefaultAsync(cancellationToken);

            BudgetItemStatus? restoreStatus = null;
            if (!string.IsNullOrWhiteSpace(previousStatusJson))
            {
                using var prior = System.Text.Json.JsonDocument.Parse(previousStatusJson);
                if (prior.RootElement.TryGetProperty("Status", out var previousValue) &&
                    Enum.TryParse<BudgetItemStatus>(previousValue.GetString(), out var parsed) &&
                    Enum.IsDefined(parsed))
                    restoreStatus = parsed;
            }
            item.UndoPurchased(restoreStatus);
        }

        dbContext.AuditEvents.Add(new AuditEvent(
            actor,
            nameof(BudgetItem),
            item.Id,
            AuditAction.Explicit,
            System.Text.Json.JsonSerializer.Serialize(new { Status = previousStatus.ToString() }),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = item.Status.ToString(),
                TrackingOnly = true,
                ApprovalAndActualsUnchanged = true
            }),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow));
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
