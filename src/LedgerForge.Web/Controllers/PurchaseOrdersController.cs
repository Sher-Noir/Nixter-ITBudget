using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("purchase-orders")]
public sealed class PurchaseOrdersController(
    ProcurementService procurementService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageProcurement)).Succeeded;
        ViewData["ErrorMessage"] = TempData["PurchaseOrderError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await procurementService.GetPurchaseOrdersAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await procurementService.GetPurchaseOrderAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageProcurement)).Succeeded;
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["ErrorMessage"] = TempData["PurchaseOrderError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("")]
    public async Task<IActionResult> Create(
        Guid fiscalYearId,
        Guid vendorId,
        string number,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await procurementService.CreatePurchaseOrderAsync(fiscalYearId, vendorId, number, description, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/lines")]
    public async Task<IActionResult> AddLine(
        Guid id,
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
            await procurementService.AddLineAsync(id, description, quantity, unitCost, budgetItemId, financeAccountId, departmentId, locationId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => procurementService.SubmitAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => procurementService.ApproveAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/issue")]
    public Task<IActionResult> Issue(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => procurementService.IssueAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => procurementService.CloseAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await procurementService.CancelAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<IActionResult> RunTransition(Guid id, Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["PurchaseOrderError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }
}
