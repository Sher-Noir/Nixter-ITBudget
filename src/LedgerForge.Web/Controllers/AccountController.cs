using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Route("account")]
public sealed class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = IsLocal(returnUrl) ? returnUrl : "/";
        return View();
    }

    [Authorize(Policy = AuthorizationPolicies.AuthenticationOnly)]
    [HttpGet("windows")]
    public IActionResult Windows(string? returnUrl = null)
        => LocalRedirect(IsLocal(returnUrl) ? returnUrl! : "/");

    private bool IsLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);
}
