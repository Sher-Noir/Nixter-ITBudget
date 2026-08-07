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
}
