using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crch.ItBudget.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

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
