using LedgerForge.Domain.Security;
using LedgerForge.Infrastructure.Security;
using LedgerForge.Web.Models.Administration;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/security")]
public sealed class AdministrationSecurityController(SecurityAdministrationService securityAdministrationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await BuildModelAsync(cancellationToken));

    [HttpPost("ad-groups")]
    public async Task<IActionResult> AddMapping(string role, string groupName, string? description, CancellationToken cancellationToken)
    {
        if (!TryParseRole(role, out var parsedRole))
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid application role."));
        try
        {
            await securityAdministrationService.AddOrActivateMappingAsync(parsedRole, groupName, description, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("ad-groups/{mappingId:guid}")]
    public async Task<IActionResult> UpdateMapping(Guid mappingId, string groupName, string? description, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            await securityAdministrationService.UpdateMappingAsync(mappingId, groupName, description, isActive, cancellationToken);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("ad-groups/{mappingId:guid}/state")]
    public async Task<IActionResult> SetMappingState(Guid mappingId, bool isActive, CancellationToken cancellationToken)
    {
        try { await securityAdministrationService.SetMappingActiveAsync(mappingId, isActive, cancellationToken); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("ad-groups/{mappingId:guid}/delete")]
    public async Task<IActionResult> DeleteMapping(Guid mappingId, CancellationToken cancellationToken)
    {
        try { await securityAdministrationService.DeleteMappingAsync(mappingId, cancellationToken); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions")]
    public async Task<IActionResult> AddUserException(string domainIdentity, string role, string effect, string reason, CancellationToken cancellationToken)
    {
        if (!TryParseRole(role, out var parsedRole))
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid application role."));
        if (!TryParseEffect(effect, out var parsedEffect))
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid exception effect."));
        try
        {
            await securityAdministrationService.AddOrUpdateUserExceptionAsync(domainIdentity, parsedRole, parsedEffect, reason, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions/{exceptionId:guid}")]
    public async Task<IActionResult> UpdateUserException(Guid exceptionId, string effect, string reason, bool isActive, CancellationToken cancellationToken)
    {
        if (!TryParseEffect(effect, out var parsedEffect))
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid exception effect."));
        try
        {
            await securityAdministrationService.UpdateUserExceptionAsync(exceptionId, parsedEffect, reason, isActive, cancellationToken);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions/{exceptionId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUserException(Guid exceptionId, CancellationToken cancellationToken)
    {
        try { await securityAdministrationService.DeactivateUserExceptionAsync(exceptionId, cancellationToken); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions/{exceptionId:guid}/delete")]
    public async Task<IActionResult> DeleteUserException(Guid exceptionId, CancellationToken cancellationToken)
    {
        try { await securityAdministrationService.DeleteUserExceptionAsync(exceptionId, cancellationToken); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Index), new { saved = true });
    }

    private async Task<SecurityMappingsViewModel> BuildModelAsync(CancellationToken cancellationToken, string? errorMessage = null)
    {
        var mappings = await securityAdministrationService.ListMappingsAsync(cancellationToken);
        var userExceptions = await securityAdministrationService.ListUserExceptionsAsync(cancellationToken);
        var successMessage = Request.Query.TryGetValue("saved", out var saved) && saved == "True" ? "Security configuration saved." : null;
        return new(mappings, userExceptions, Enum.GetValues<ApplicationRole>(), Enum.GetValues<UserRoleExceptionEffect>(), errorMessage, successMessage);
    }

    private static bool TryParseRole(string role, out ApplicationRole parsedRole)
        => Enum.TryParse(role, ignoreCase: false, out parsedRole) && Enum.IsDefined(parsedRole);

    private static bool TryParseEffect(string effect, out UserRoleExceptionEffect parsedEffect)
        => Enum.TryParse(effect, ignoreCase: false, out parsedEffect) && Enum.IsDefined(parsedEffect);
}
