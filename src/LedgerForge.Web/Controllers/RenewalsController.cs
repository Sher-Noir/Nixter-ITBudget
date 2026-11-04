using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("renewals")]
public sealed class RenewalsController(
    RenewalCalendarService renewalCalendarService,
    NextFiscalYearRenewalPlanningService planningService,
    FiscalYearAdministrationService fiscalYearService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? fiscalYearId, CancellationToken cancellationToken)
        => View(await renewalCalendarService.GetAsync(fiscalYearId, cancellationToken));

    [HttpGet("plan")]
    public async Task<IActionResult> Plan(
        Guid? sourceFiscalYearId, Guid? targetFiscalYearId,
        Guid? targetVersionId, decimal upliftPercent,
        CancellationToken cancellationToken)
    {
        ViewData["CanPlan"] = (await authorizationService.AuthorizeAsync(
            User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        ViewData["CanCreateYear"] = (await authorizationService.AuthorizeAsync(
            User, AuthorizationPolicies.ManageFiscalYears)).Succeeded;
        ViewData["Error"] = TempData["RenewalPlannerError"] as string;
        ViewData["Created"] = Request.Query["created"].ToString();
        return View(await planningService.GetAsync(
            sourceFiscalYearId, targetFiscalYearId, targetVersionId,
            upliftPercent, cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageFiscalYears)]
    [HttpPost("plan/create-fiscal-year")]
    public async Task<IActionResult> CreateNextFiscalYear(
        Guid sourceFiscalYearId, string displayName, DateOnly startDate,
        DateOnly endDate, int planningYear, string initialVersionName,
        CancellationToken cancellationToken)
    {
        try
        {
            var years = await fiscalYearService.ListAsync(cancellationToken);
            var source = years.SingleOrDefault(x => x.Id == sourceFiscalYearId)
                ?? throw new KeyNotFoundException("Source fiscal year was not found.");
            if (startDate <= source.EndDate)
                throw new ArgumentException("The following fiscal year must start after the source year.");
            var targetId = await fiscalYearService.CreateAsync(
                displayName, startDate, endDate, planningYear,
                "Initial renewal and budget planning.", isCurrent: false,
                createMonthlyPeriods: false, initialVersionName, cancellationToken);
            return RedirectToAction(nameof(Plan), new
            {
                sourceFiscalYearId, targetFiscalYearId = targetId
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["RenewalPlannerError"] = exception.Message;
            return RedirectToAction(nameof(Plan), new { sourceFiscalYearId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("plan")]
    public async Task<IActionResult> CreateRenewalDrafts(
        Guid sourceFiscalYearId, Guid targetFiscalYearId, Guid targetVersionId,
        decimal upliftPercent, string[] selectedKeys, bool confirmSelection,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!confirmSelection)
                throw new ArgumentException("Review and confirm the selected renewals before creating drafts.");
            var count = await planningService.CreateDraftsAsync(
                sourceFiscalYearId, targetFiscalYearId, targetVersionId,
                upliftPercent, selectedKeys, cancellationToken);
            return RedirectToAction(nameof(Plan), new
            {
                sourceFiscalYearId, targetFiscalYearId, targetVersionId,
                upliftPercent, created = count
            });
        }
        catch (Exception exception) when (exception is ArgumentException or
            InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            TempData["RenewalPlannerError"] = exception.Message;
            return RedirectToAction(nameof(Plan), new
            {
                sourceFiscalYearId, targetFiscalYearId, targetVersionId, upliftPercent
            });
        }
    }
}
