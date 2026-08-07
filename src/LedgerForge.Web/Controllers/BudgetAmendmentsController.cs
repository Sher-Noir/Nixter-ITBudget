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
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanEdit"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["ErrorMessage"] = TempData["AmendmentError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await amendmentService.GetAsync(cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("")]
    public async Task<IActionResult> Create(Guid budgetItemId, decimal amountDelta, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await amendmentService.CreateAsync(budgetItemId, amountDelta, reason, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => Run(id, () => amendmentService.SubmitAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, string? note, CancellationToken cancellationToken)
    {
        try
        {
            await amendmentService.ApproveAsync(id, RequireActor(), note, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, string note, CancellationToken cancellationToken)
    {
        try
        {
            await amendmentService.RejectAsync(id, RequireActor(), note, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await amendmentService.CancelAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["AmendmentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<IActionResult> Run(Guid id, Func<Task> action)
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
