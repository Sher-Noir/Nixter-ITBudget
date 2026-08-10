using LedgerForge.Infrastructure.Dashboard;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

public sealed class HomeController(
    DashboardService dashboardService,
    IAuthorizationService authorizationService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? fiscalYearId,
        Guid? locationId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });

        if (!(await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ViewBudget)).Succeeded)
            return Forbid();

        ViewData["CanManageImports"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageImports)).Succeeded;
        return View(await dashboardService.GetAsync(fiscalYearId, locationId, categoryId, cancellationToken));
    }

    // Error/status re-execution preserves the original HTTP method. These endpoints
    // intentionally accept any verb so a failing POST is not incorrectly converted
    // into HTTP 405 while the real 4xx/5xx status is being rendered.
    [AllowAnonymous]
    public IActionResult AccessDenied(int code = StatusCodes.Status403Forbidden)
    {
        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        return View("AccessDenied");
    }

    [AllowAnonymous]
    public IActionResult Error() => View("Error");
}