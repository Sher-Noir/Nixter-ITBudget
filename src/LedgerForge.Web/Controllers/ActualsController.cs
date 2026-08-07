using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("actuals")]
public sealed class ActualsController(
    ActualLedgerService actualLedgerService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? fiscalYearId, CancellationToken cancellationToken)
    {
        ViewData["CanPostActuals"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.PostActuals)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ActualsError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        ViewData["Reversed"] = Request.Query.ContainsKey("reversed");
        return View(await actualLedgerService.GetAsync(fiscalYearId, cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.PostActuals)]
    [HttpPost("manual")]
    public async Task<IActionResult> PostManual(
        Guid fiscalYearId,
        DateOnly transactionDate,
        decimal amount,
        string description,
        string? sourceReference,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken)
    {
        try
        {
            await actualLedgerService.PostManualAsync(
                fiscalYearId,
                transactionDate,
                amount,
                description,
                sourceReference,
                budgetItemId,
                financeAccountId,
                departmentId,
                locationId,
                fiscalPeriodId,
                cancellationToken);
            return RedirectToAction(nameof(Index), new { fiscalYearId, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["ActualsError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PostActuals)]
    [HttpPost("{id:guid}/reverse")]
    public async Task<IActionResult> Reverse(
        Guid id,
        Guid fiscalYearId,
        DateOnly reversalDate,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await actualLedgerService.ReverseAsync(id, reversalDate, reason, cancellationToken);
            return RedirectToAction(nameof(Index), new { fiscalYearId, reversed = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["ActualsError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
    }
}
