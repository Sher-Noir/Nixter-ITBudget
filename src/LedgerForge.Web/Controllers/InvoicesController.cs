using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("invoices")]
public sealed class InvoicesController(
    InvoiceQueryService queryService,
    InvoiceWorkflowService workflowService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageProcurement)).Succeeded;
        ViewData["ErrorMessage"] = TempData["InvoiceError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await queryService.GetIndexAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await queryService.GetAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageProcurement)).Succeeded;
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["CanPostActuals"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.PostActuals)).Succeeded;
        ViewData["ErrorMessage"] = TempData["InvoiceError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        ViewData["Posted"] = Request.Query.ContainsKey("posted");
        return View(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("")]
    public async Task<IActionResult> Create(
        Guid fiscalYearId, Guid vendorId, string invoiceNumber, DateOnly invoiceDate,
        string description, decimal totalAmount, Guid? purchaseOrderId, CancellationToken cancellationToken)
    {
        try
        {
            var id = await workflowService.CreateAsync(fiscalYearId, vendorId, invoiceNumber, invoiceDate, description, totalAmount, purchaseOrderId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/allocations")]
    public async Task<IActionResult> AddAllocation(
        Guid id, string description, decimal amount, Guid? budgetItemId, Guid? financeAccountId,
        Guid? departmentId, Guid? locationId, Guid? fiscalPeriodId, CancellationToken cancellationToken)
    {
        try
        {
            await workflowService.AddAllocationAsync(id, description, amount, budgetItemId, financeAccountId, departmentId, locationId, fiscalPeriodId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/submit")]
    public Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => workflowService.SubmitAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        => RunTransition(id, () => workflowService.ApproveAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await workflowService.RejectAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PostActuals)]
    [HttpPost("{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await workflowService.CommitToLedgerAsync(id, RequireActor(), cancellationToken);
            return RedirectToAction(nameof(Details), new { id, posted = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await workflowService.CancelAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
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
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["InvoiceError"] = ex.Message;
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
