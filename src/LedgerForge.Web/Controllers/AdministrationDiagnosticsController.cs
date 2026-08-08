using LedgerForge.Web.Diagnostics;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.Administration)]
[Route("admin/diagnostics")]
public sealed class AdministrationDiagnosticsController(DeploymentDiagnosticsService diagnosticsService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await diagnosticsService.GetAsync(cancellationToken));
}
