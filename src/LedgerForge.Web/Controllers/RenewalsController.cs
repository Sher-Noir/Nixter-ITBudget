using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("renewals")]
public sealed class RenewalsController(RenewalCalendarService renewalCalendarService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? fiscalYearId, CancellationToken cancellationToken)
        => View(await renewalCalendarService.GetAsync(fiscalYearId, cancellationToken));
}
