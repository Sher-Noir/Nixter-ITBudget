using Crch.ItBudget.Domain.Security;
using Crch.ItBudget.Infrastructure.Security;
using Crch.ItBudget.Web.Models.Administration;
using Crch.ItBudget.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crch.ItBudget.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/security")]
public sealed class AdministrationSecurityController(
    SecurityAdministrationService securityAdministrationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildModelAsync(cancellationToken));
    }

    [HttpPost("ad-groups")]
    public async Task<IActionResult> AddMapping(
        string role,
        string groupName,
        string? description,
        CancellationToken cancellationToken)
    {
        if (!TryParseRole(role, out var parsedRole))
        {
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid application role."));
        }

        try
        {
            await securityAdministrationService.AddOrActivateMappingAsync(
                parsedRole,
                groupName,
                description,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }

        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("ad-groups/{mappingId:guid}/state")]
    public async Task<IActionResult> SetMappingState(
        Guid mappingId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            await securityAdministrationService.SetMappingActiveAsync(mappingId, isActive, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions")]
    public async Task<IActionResult> AddUserException(
        string domainIdentity,
        string role,
        string effect,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryParseRole(role, out var parsedRole))
        {
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid application role."));
        }

        if (!Enum.TryParse<UserRoleExceptionEffect>(effect, ignoreCase: false, out var parsedEffect) || !Enum.IsDefined(parsedEffect))
        {
            return View("Index", await BuildModelAsync(cancellationToken, "Select a valid exception effect."));
        }

        try
        {
            await securityAdministrationService.AddOrUpdateUserExceptionAsync(
                domainIdentity,
                parsedRole,
                parsedEffect,
                reason,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return View("Index", await BuildModelAsync(cancellationToken, exception.Message));
        }

        return RedirectToAction(nameof(Index), new { saved = true });
    }

    [HttpPost("user-exceptions/{exceptionId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUserException(
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await securityAdministrationService.DeactivateUserExceptionAsync(exceptionId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index), new { saved = true });
    }

    private async Task<SecurityMappingsViewModel> BuildModelAsync(
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        var mappings = await securityAdministrationService.ListMappingsAsync(cancellationToken);
        var userExceptions = await securityAdministrationService.ListUserExceptionsAsync(cancellationToken);
        var roles = Enum.GetValues<ApplicationRole>();
        var effects = Enum.GetValues<UserRoleExceptionEffect>();
        var successMessage = Request.Query.TryGetValue("saved", out var saved) && saved == "True"
            ? "Security configuration saved."
            : null;

        return new(mappings, userExceptions, roles, effects, errorMessage, successMessage);
    }

    private static bool TryParseRole(string role, out ApplicationRole parsedRole)
    {
        return Enum.TryParse(role, ignoreCase: false, out parsedRole) && Enum.IsDefined(parsedRole);
    }
}
