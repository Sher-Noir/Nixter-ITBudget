using LedgerForge.Infrastructure.Operations;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("work")]
public sealed class WorkController(
    WorkQueueService workQueueService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanApprove"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.Approve)).Succeeded;
        ViewData["CanManageImports"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageImports)).Succeeded;
        ViewData["CanManageBudget"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageBudget)).Succeeded;
        return View(await workQueueService.GetAsync(cancellationToken));
    }
}
