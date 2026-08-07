using LedgerForge.Web.Configuration;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/organization")]
public sealed class AdministrationOrganizationController(
    OrganizationSettingsStore settingsStore) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await settingsStore.GetAsync(cancellationToken);
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        ViewData["Reset"] = Request.Query.ContainsKey("reset");
        return View(model);
    }

    [HttpPost("")]
    public async Task<IActionResult> Save(
        BrandingOptions model,
        CancellationToken cancellationToken)
    {
        try
        {
            await settingsStore.SaveAsync(model, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", model);
        }

        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken)
    {
        await settingsStore.ResetAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { reset = true });
    }
}
