using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
[Route("purchase-orders/{purchaseOrderId:guid}/lines")]
public sealed class PurchaseOrderLinesController(LedgerForgeDbContext dbContext) : Controller
{
    [HttpPost("{lineId:guid}/update")]
    public async Task<IActionResult> Update(
        Guid purchaseOrderId,
        Guid lineId,
        string description,
        decimal quantity,
        decimal unitCost,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await RequireDraftOrderAsync(purchaseOrderId, cancellationToken);
            var line = await dbContext.PurchaseOrderLines.SingleOrDefaultAsync(
                x => x.Id == lineId && x.PurchaseOrderId == purchaseOrderId,
                cancellationToken) ?? throw new KeyNotFoundException("Purchase order line was not found.");

            if (budgetItemId is Guid itemId)
            {
                if (itemId == Guid.Empty || !await dbContext.BudgetItems.AsNoTracking().AnyAsync(
                        x => x.Id == itemId && x.FiscalYearId == order.FiscalYearId &&
                             x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived,
                        cancellationToken))
                    throw new InvalidOperationException("Selected budget item does not belong to this fiscal year or is inactive.");
            }
            await RequireLookupAsync(dbContext.FinanceAccounts, financeAccountId, "finance account", cancellationToken);
            await RequireLookupAsync(dbContext.Departments, departmentId, "department", cancellationToken);
            await RequireLookupAsync(dbContext.Locations, locationId, "location", cancellationToken);

            // A blank selection in the edit form means "keep the current coding". This is
            // important for historical dimensions that may have since been deactivated.
            budgetItemId ??= line.BudgetItemId;
            financeAccountId ??= line.FinanceAccountId;
            departmentId ??= line.DepartmentId;
            locationId ??= line.LocationId;

            line.Update(description, quantity, unitCost, budgetItemId, financeAccountId, departmentId, locationId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToAction("Details", "PurchaseOrders", new { id = purchaseOrderId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction("Details", "PurchaseOrders", new { id = purchaseOrderId });
        }
    }

    [HttpPost("{lineId:guid}/delete")]
    public async Task<IActionResult> Delete(Guid purchaseOrderId, Guid lineId, CancellationToken cancellationToken)
    {
        try
        {
            _ = await RequireDraftOrderAsync(purchaseOrderId, cancellationToken);
            var line = await dbContext.PurchaseOrderLines.SingleOrDefaultAsync(
                x => x.Id == lineId && x.PurchaseOrderId == purchaseOrderId,
                cancellationToken) ?? throw new KeyNotFoundException("Purchase order line was not found.");
            dbContext.PurchaseOrderLines.Remove(line);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToAction("Details", "PurchaseOrders", new { id = purchaseOrderId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction("Details", "PurchaseOrders", new { id = purchaseOrderId });
        }
    }

    private async Task<PurchaseOrder> RequireDraftOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == purchaseOrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Purchase order was not found.");
        if (order.State != PurchaseOrderState.Draft)
            throw new InvalidOperationException("Purchase order lines can only be edited while the purchase order is Draft.");
        return order;
    }

    private static async Task RequireLookupAsync<TEntity>(
        DbSet<TEntity> set,
        Guid? id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (id is null) return;
        if (id == Guid.Empty || !await set.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, cancellationToken))
            throw new InvalidOperationException($"Selected {label} does not exist or is inactive.");
    }
}
