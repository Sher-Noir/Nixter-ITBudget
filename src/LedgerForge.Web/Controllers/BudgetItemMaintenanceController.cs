using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Web.Documents;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
[Route("budget/items")]
public sealed class BudgetItemMaintenanceController(
    LedgerForgeDbContext dbContext,
    IWebHostEnvironment environment,
    IConfiguration configuration) : Controller
{
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.BudgetItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return NotFound();

        var fiscalYearId = item.FiscalYearId;
        var versionId = item.BudgetVersionId;
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        if (version is null) return NotFound();

        if (version.IsLocked || !item.IsPlanningEditable)
        {
            TempData["BudgetItemError"] = "Only items in an unlocked Draft, Proposed, or Deferred planning state can be deleted. Submitted or historical items must remain in the workflow record.";
            return Redirect($"/budget/items/{id}");
        }

        var configuredLimit = configuration.GetValue<long?>("Documents:MaxFileSizeBytes");
        var configuredStoragePath = configuration.GetValue<string>("Documents:StoragePath");
        var documentStore = new PhysicalDocumentStore(
            environment.ContentRootPath,
            configuredStoragePath,
            configuredLimit is > 0 ? configuredLimit.Value : 25L * 1024 * 1024);
        var hasDocuments = (await documentStore.ListAsync(cancellationToken))
            .Any(x => string.Equals(x.LinkedEntityType, nameof(BudgetItem), StringComparison.OrdinalIgnoreCase) && x.LinkedEntityId == id);
        if (hasDocuments)
        {
            TempData["BudgetItemError"] = "Remove linked documents and the item-specific logo before deleting this planning item.";
            return Redirect($"/budget/items/{id}");
        }

        dbContext.BudgetItems.Remove(item);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsForeignKeyReference(exception))
        {
            dbContext.ChangeTracker.Clear();
            TempData["BudgetItemError"] = "This planning item is already referenced by financial or workflow history and cannot be deleted. Keep it as part of the audit trail instead.";
            return Redirect($"/budget/items/{id}");
        }

        TempData["BudgetNotice"] = $"Budget item '{item.ItemNumber}' was deleted.";
        return Redirect($"/budget?fiscalYearId={fiscalYearId}&versionId={versionId}");
    }

    private static bool IsForeignKeyReference(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 547 }) return true;
        }
        return false;
    }
}
