using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Models.Budgeting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("budget")]
public sealed class BudgetController(
    BudgetPlanningService planningService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid? fiscalYearId,
        Guid? versionId,
        CancellationToken cancellationToken)
    {
        var snapshot = await planningService.GetSnapshotAsync(fiscalYearId, versionId, cancellationToken);
        var canEdit = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        var errorMessage = TempData["BudgetError"] as string;

        return View(new BudgetPlanningViewModel(
            snapshot,
            canEdit,
            errorMessage,
            Request.Query.ContainsKey("saved")));
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("quick-add")]
    public async Task<IActionResult> QuickAdd(
        Guid fiscalYearId,
        Guid versionId,
        string itemNumber,
        string description,
        decimal quantity,
        decimal unitCost,
        CancellationToken cancellationToken)
    {
        try
        {
            await planningService.AddItemAsync(
                fiscalYearId,
                versionId,
                itemNumber,
                description,
                quantity,
                unitCost,
                cancellationToken);

            return RedirectToAction(nameof(Index), new { fiscalYearId, versionId, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["BudgetError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fiscalYearId, versionId });
        }
    }

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> Item(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await planningService.GetItemAsync(id, cancellationToken);
        if (snapshot is null) return NotFound();

        var canEditRole = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        return View("Item", new BudgetItemEditViewModel(
            snapshot,
            canEditRole && !snapshot.VersionLocked,
            TempData["BudgetItemError"] as string,
            Request.Query.ContainsKey("saved")));
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("items/{id:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        string itemNumber,
        string description,
        string? reasonPurpose,
        PurchaseType purchaseType,
        decimal quantity,
        decimal unitCost,
        DateOnly? estimatedPurchaseDate,
        DateOnly? renewalDate,
        Guid? budgetSectionId,
        Guid? financeTypeId,
        Guid? departmentId,
        Guid? locationId,
        Guid? needLevelId,
        Guid? internalCategoryId,
        Guid? frequencyId,
        CancellationToken cancellationToken)
    {
        try
        {
            await planningService.UpdateItemAsync(
                id,
                itemNumber,
                description,
                reasonPurpose,
                purchaseType,
                quantity,
                unitCost,
                estimatedPurchaseDate,
                renewalDate,
                budgetSectionId,
                financeTypeId,
                departmentId,
                locationId,
                needLevelId,
                internalCategoryId,
                frequencyId,
                cancellationToken);

            return RedirectToAction(nameof(Item), new { id, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["BudgetItemError"] = exception.Message;
            return RedirectToAction(nameof(Item), new { id });
        }
    }
}
