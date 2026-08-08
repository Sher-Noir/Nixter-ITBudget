using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("reports")]
public sealed class ReportsController(ReportingService reportingService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? fiscalYearId, CancellationToken cancellationToken)
        => View(await reportingService.GetCenterAsync(fiscalYearId, cancellationToken));
}
