using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Approve)]
[Route("approvals")]
public sealed class ApprovalsController(ApprovalQueueService approvalQueueService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ErrorMessage"] = TempData["ApprovalError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await approvalQueueService.GetAsync(cancellationToken));
    }

    [HttpPost("budget-items/{id:guid}/approve")]
    public Task<IActionResult> ApproveBudgetItem(Guid id, string? note, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.ApproveBudgetItemAsync(id, RequireActor(), note, cancellationToken));

    [HttpPost("budget-items/{id:guid}/deny")]
    public Task<IActionResult> DenyBudgetItem(Guid id, string reason, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.DenyBudgetItemAsync(id, RequireActor(), reason, cancellationToken));

    [HttpPost("budget-items/{id:guid}/defer")]
    public Task<IActionResult> DeferBudgetItem(Guid id, string reason, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.DeferBudgetItemAsync(id, RequireActor(), reason, cancellationToken));

    [HttpPost("purchase-orders/{id:guid}/approve")]
    public Task<IActionResult> ApprovePurchaseOrder(Guid id, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.ApprovePurchaseOrderAsync(id, RequireActor(), cancellationToken));

    [HttpPost("purchase-orders/{id:guid}/reject")]
    public Task<IActionResult> RejectPurchaseOrder(Guid id, string reason, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.RejectPurchaseOrderAsync(id, RequireActor(), reason, cancellationToken));

    [HttpPost("invoices/{id:guid}/approve")]
    public Task<IActionResult> ApproveInvoice(Guid id, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.ApproveInvoiceAsync(id, RequireActor(), cancellationToken));

    [HttpPost("invoices/{id:guid}/reject")]
    public Task<IActionResult> RejectInvoice(Guid id, string reason, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.RejectInvoiceAsync(id, RequireActor(), reason, cancellationToken));

    [HttpPost("budget-amendments/{id:guid}/approve")]
    public Task<IActionResult> ApproveBudgetAmendment(Guid id, string? note, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.ApproveBudgetAmendmentAsync(id, RequireActor(), note, cancellationToken));

    [HttpPost("budget-amendments/{id:guid}/reject")]
    public Task<IActionResult> RejectBudgetAmendment(Guid id, string reason, CancellationToken cancellationToken)
        => RunDecision(() => approvalQueueService.RejectBudgetAmendmentAsync(id, RequireActor(), reason, cancellationToken));

    private async Task<IActionResult> RunDecision(Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ApprovalError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor))
            throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }
}
