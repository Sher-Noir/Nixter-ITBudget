using LedgerForge.Domain.Auditing;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetItemPurchaseOrderSummary(
    Guid Id,
    string Number,
    string Vendor,
    PurchaseOrderState State,
    decimal Amount);

public sealed record BudgetItemInvoiceSummary(
    Guid Id,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string Vendor,
    InvoiceState State,
    decimal Amount);

public sealed record BudgetItemActualSummary(
    Guid Id,
    DateOnly TransactionDate,
    string Description,
    decimal Amount,
    string? SourceReference);

public sealed record BudgetItemActivitySummary(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    AuditAction Action);

public sealed record BudgetItemWorkspaceSnapshot(
    BudgetItemEditSnapshot Item,
    decimal CurrentBudget,
    decimal Committed,
    decimal Actual,
    decimal Available,
    decimal Forecast,
    IReadOnlyList<BudgetItemPurchaseOrderSummary> PurchaseOrders,
    IReadOnlyList<BudgetItemInvoiceSummary> Invoices,
    IReadOnlyList<BudgetItemActualSummary> Actuals,
    IReadOnlyList<BudgetItemActivitySummary> Activity);

public sealed class BudgetItemWorkspaceService(
    LedgerForgeDbContext dbContext,
    BudgetPlanningService planningService)
{
    public async Task<BudgetItemWorkspaceSnapshot?> GetAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await planningService.GetItemAsync(itemId, cancellationToken);
        if (item is null) return null;

        var purchaseOrderRows = await (
            from line in dbContext.PurchaseOrderLines.AsNoTracking()
            join order in dbContext.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
            join vendor in dbContext.Vendors.AsNoTracking() on order.VendorId equals vendor.Id
            where line.BudgetItemId == itemId
            select new
            {
                order.Id,
                order.Number,
                order.State,
                Vendor = vendor.Name,
                line.LineTotal
            })
            .ToListAsync(cancellationToken);

        var purchaseOrders = purchaseOrderRows
            .GroupBy(x => new { x.Id, x.Number, x.State, x.Vendor })
            .Select(group => new BudgetItemPurchaseOrderSummary(
                group.Key.Id,
                group.Key.Number,
                group.Key.Vendor,
                group.Key.State,
                group.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.State == PurchaseOrderState.Issued)
            .ThenBy(x => x.Number)
            .ToArray();

        var invoiceRows = await (
            from allocation in dbContext.InvoiceAllocations.AsNoTracking()
            join invoice in dbContext.Invoices.AsNoTracking() on allocation.InvoiceId equals invoice.Id
            join vendor in dbContext.Vendors.AsNoTracking() on invoice.VendorId equals vendor.Id
            where allocation.BudgetItemId == itemId
            select new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.State,
                Vendor = vendor.Name,
                allocation.Amount
            })
            .ToListAsync(cancellationToken);

        var invoices = invoiceRows
            .GroupBy(x => new { x.Id, x.InvoiceNumber, x.InvoiceDate, x.State, x.Vendor })
            .Select(group => new BudgetItemInvoiceSummary(
                group.Key.Id,
                group.Key.InvoiceNumber,
                group.Key.InvoiceDate,
                group.Key.Vendor,
                group.Key.State,
                group.Sum(x => x.Amount)))
            .OrderByDescending(x => x.InvoiceDate)
            .ThenBy(x => x.InvoiceNumber)
            .ToArray();

        var actuals = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.BudgetItemId == itemId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .Select(x => new BudgetItemActualSummary(
                x.Id,
                x.TransactionDate,
                x.Description,
                x.Amount,
                x.SourceReference))
            .ToListAsync(cancellationToken);

        var activity = await dbContext.AuditEvents.AsNoTracking()
            .Where(x => x.EntityId == itemId && x.EntityType == nameof(BudgetItem))
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(12)
            .Select(x => new BudgetItemActivitySummary(x.Id, x.OccurredAtUtc, x.Actor, x.Action))
            .ToListAsync(cancellationToken);

        var currentBudget = item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
        var issuedCommitment = purchaseOrders
            .Where(x => x.State == PurchaseOrderState.Issued)
            .Sum(x => x.Amount);
        var postedInvoiceAmount = invoices
            .Where(x => x.State == InvoiceState.Posted)
            .Sum(x => x.Amount);
        var committed = Math.Max(0m, issuedCommitment - postedInvoiceAmount);
        var actual = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.BudgetItemId == itemId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var available = currentBudget - committed - actual;

        var publishedScenarioId = await dbContext.ForecastScenarios.AsNoTracking()
            .Where(x => x.FiscalYearId == item.FiscalYearId &&
                        x.BudgetVersionId == item.BudgetVersionId &&
                        x.State == ForecastScenarioState.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.AsOfDate)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        decimal? publishedForecast = null;
        if (publishedScenarioId is not null)
        {
            publishedForecast = await dbContext.ForecastLines.AsNoTracking()
                .Where(x => x.ForecastScenarioId == publishedScenarioId.Value && x.BudgetItemId == itemId)
                .Select(x => (decimal?)x.ForecastTotal)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var forecast = publishedForecast ?? Math.Max(currentBudget, committed + actual);

        return new(
            item,
            currentBudget,
            committed,
            actual,
            available,
            forecast,
            purchaseOrders,
            invoices,
            actuals,
            activity);
    }
}
