using LedgerForge.Infrastructure.Dashboard;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
public sealed class HomeController(
    DashboardService dashboardService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageImports"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageImports)).Succeeded;
        return View(await dashboardService.GetAsync(cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied(int code = StatusCodes.Status403Forbidden)
    {
        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        return View("AccessDenied");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error() => View("Error");
}
