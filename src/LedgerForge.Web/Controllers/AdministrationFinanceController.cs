using LedgerForge.Infrastructure.MasterData;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/finance")]
public sealed class AdministrationFinanceController(FinanceAdministrationService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["ErrorMessage"] = TempData["FinanceError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await service.GetAsync(cancellationToken));
    }

    [HttpPost("accounts")]
    public async Task<IActionResult> AddAccount(
        string code,
        string name,
        string? description,
        Guid? financeCategoryId,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.AddAccountAsync(code, name, description, financeCategoryId, sortOrder, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["FinanceError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        string name,
        string? description,
        Guid? financeCategoryId,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.UpdateAccountAsync(id, name, description, financeCategoryId, sortOrder, isActive, cancellationToken);
            return RedirectToAction(nameof(Index), new { saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["FinanceError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}
