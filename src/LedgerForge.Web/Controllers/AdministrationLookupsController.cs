using LedgerForge.Infrastructure.MasterData;
using LedgerForge.Infrastructure.Persistence.Seeding;
using LedgerForge.Web.Models.Administration;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/lookups")]
public sealed class AdministrationLookupsController(
    LookupAdministrationService service,
    ManagedLookupInitializer initializer,
    ConfigurationDeletionService deletionService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? type, CancellationToken cancellationToken)
    {
        LookupAdministrationSnapshot snapshot;
        try
        {
            snapshot = await service.GetAsync(type, cancellationToken);
        }
        catch (ArgumentException)
        {
            snapshot = await service.GetAsync(null, cancellationToken);
        }

        ViewData["Notice"] = TempData["LookupNotice"] as string;
        return View(new LookupAdministrationViewModel(
            snapshot,
            TempData["LookupError"] as string,
            Request.Query.ContainsKey("saved"),
            Request.Query.ContainsKey("initialized")));
    }

    [HttpPost("values")]
    public async Task<IActionResult> Add(
        string type,
        string code,
        string name,
        string? description,
        int sortOrder,
        int? numericValue,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.AddAsync(type, code, name, description, sortOrder, numericValue, cancellationToken);
            return RedirectToAction(nameof(Index), new { type, saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["LookupError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { type });
        }
    }

    [HttpPost("values/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        string type,
        string name,
        string? description,
        int sortOrder,
        int? numericValue,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.UpdateAsync(type, id, name, description, sortOrder, numericValue, isActive, cancellationToken);
            return RedirectToAction(nameof(Index), new { type, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["LookupError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { type });
        }
    }

    [HttpPost("values/{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, string type, CancellationToken cancellationToken)
    {
        try
        {
            var result = await deletionService.DeleteLookupAsync(type, id, cancellationToken);
            TempData["LookupNotice"] = result.Message;
            return RedirectToAction(nameof(Index), new { type });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["LookupError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { type });
        }
    }

    [HttpPost("initialize-defaults")]
    public async Task<IActionResult> InitializeDefaults(string? type, CancellationToken cancellationToken)
    {
        await initializer.InitializeMissingAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { type, initialized = true });
    }
}
