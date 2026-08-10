using LedgerForge.Web.Models.Administration;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("profile")]
public sealed class ProfileController(
    ModuleAccessResolver moduleAccessResolver,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var identity = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(identity)) return Challenge();

        var configurable = await moduleAccessResolver.ResolveAsync(User, cancellationToken);
        var rows = new List<ProfileModuleAccessRow>();
        foreach (var module in Enum.GetValues<LedgerForgeModule>())
        {
            var level = ModuleAccessLevel.None;
            foreach (var candidate in new[] { ModuleAccessLevel.Admin, ModuleAccessLevel.Manage, ModuleAccessLevel.Edit, ModuleAccessLevel.View })
            {
                if ((await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ModulePolicy(module, candidate))).Succeeded)
                {
                    level = candidate;
                    break;
                }
            }
            rows.Add(new ProfileModuleAccessRow(module, level));
        }

        return View(new ProfileViewModel(
            identity,
            configurable.Roles.Select(x => x.Name).ToArray(),
            rows));
    }
}
