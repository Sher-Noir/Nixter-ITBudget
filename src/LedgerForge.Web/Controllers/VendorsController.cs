using LedgerForge.Infrastructure.MasterData;
using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("vendors")]
public sealed class VendorsController(
    ProcurementService procurementService,
    ConfigurationDeletionService deletionService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageProcurement"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageProcurement)).Succeeded;
        ViewData["ErrorMessage"] = TempData["VendorError"] as string;
        ViewData["Notice"] = TempData["VendorNotice"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await procurementService.ListVendorsAsync(cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("")]
    public async Task<IActionResult> Add(
        string code,
        string name,
        string? contactName,
        string? email,
        string? phone,
        CancellationToken cancellationToken)
    {
        try
        {
            await procurementService.AddVendorAsync(code, name, contactName, email, phone, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["VendorError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        string name,
        string? contactName,
        string? email,
        string? phone,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await procurementService.UpdateVendorAsync(id, name, contactName, email, phone, isActive, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["VendorError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageProcurement)]
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await deletionService.DeleteVendorAsync(id, cancellationToken);
            TempData["VendorNotice"] = result.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["VendorError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
