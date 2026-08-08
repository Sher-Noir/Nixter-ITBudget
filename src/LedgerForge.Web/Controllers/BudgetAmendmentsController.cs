using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("budget/amendments")]
public sealed class BudgetAmendmentsController(
    BudgetAmendmentService amendmentService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? budgetItemId, CancellationToken cancellationToken)
    {
        ViewData["CanEdit"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["ErrorMessage"] = TempData["AmendmentError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        var snapshot = await amendmentService.GetAsync(cancellationToken);
        ViewData["SelectedBudgetItemId"] = budgetItemId is not null && snapshot.EligibleBudgetItems.Any(x => x.Id == budgetItemId.Value)
            ? budgetItemId
            : null;
        return View(snapshot);
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("")]
    public async Task<IActionResult> Create(Guid budgetItemId, decimal amountDelta, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await amendmentService.CreateAsync(budgetItemId, amountDelta, reason, cancellationToken);
            return RedirectToAction(nameof(Index), new { budgetItemId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { budgetItemId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => Run(() => amendmentService.SubmitAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, string? note, CancellationToken cancellationToken)
        => Run(() => amendmentService.ApproveAsync(id, RequireActor(), note, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, string note, CancellationToken cancellationToken)
        => Run(() => amendmentService.RejectAsync(id, RequireActor(), note, cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, string reason, CancellationToken cancellationToken)
        => Run(() => amendmentService.CancelAsync(id, RequireActor(), reason, cancellationToken));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }
}
