using LedgerForge.Infrastructure.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize]
public sealed class HomeController(DashboardService dashboardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await dashboardService.GetAsync(cancellationToken));

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
