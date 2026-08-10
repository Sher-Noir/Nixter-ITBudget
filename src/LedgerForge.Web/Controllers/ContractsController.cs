using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("contracts")]
public sealed class ContractsController(
    ContractService contractService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageContracts)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ContractError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await contractService.GetIndexAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await contractService.GetAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageContracts)).Succeeded;
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ContractError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.ManageContracts)]
    [HttpPost("")]
    public async Task<IActionResult> Create(
        Guid vendorId,
        string contractNumber,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        decimal estimatedAnnualAmount,
        bool autoRenew,
        int renewalNoticeDays,
        string? description,
        Guid? budgetItemId,
        Guid? financeAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await contractService.CreateAsync(
                vendorId, contractNumber, name, startDate, endDate, estimatedAnnualAmount,
                autoRenew, renewalNoticeDays, description, budgetItemId, financeAccountId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ContractError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageContracts)]
    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => RunContractAction(id, () => contractService.ActivateAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageContracts)]
    [HttpPost("{id:guid}/terminate")]
    public async Task<IActionResult> Terminate(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await contractService.TerminateAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ContractError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.Approve)]
    [HttpPost("{contractId:guid}/renewals/{renewalId:guid}/decision")]
    public async Task<IActionResult> DecideRenewal(
        Guid contractId,
        Guid renewalId,
        string status,
        string note,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ContractRenewalStatus>(status, false, out var parsed) ||
            parsed is ContractRenewalStatus.PendingReview or ContractRenewalStatus.Completed)
        {
            TempData["ContractError"] = "Select a valid renewal decision.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }

        try
        {
            await contractService.DecideRenewalAsync(contractId, renewalId, parsed, RequireActor(), note, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = contractId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ContractError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageContracts)]
    [HttpPost("{contractId:guid}/renewals/{renewalId:guid}/complete")]
    public async Task<IActionResult> CompleteRenewal(
        Guid contractId,
        Guid renewalId,
        string note,
        CancellationToken cancellationToken)
    {
        try
        {
            await contractService.CompleteRenewalAsync(contractId, renewalId, RequireActor(), note, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = contractId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ContractError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
    }

    private async Task<IActionResult> RunContractAction(Guid id, Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ContractError"] = exception.Message;
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