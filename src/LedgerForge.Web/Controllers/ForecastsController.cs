using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("forecasts")]
public sealed class ForecastsController(
    ForecastService forecastService,
    IAuthorizationService authorizationService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanEdit"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        ViewData["CanPublish"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageBudget)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ForecastError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await forecastService.GetIndexAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await forecastService.GetAsync(id, cancellationToken);
        if (detail is null) return NotFound();
        ViewData["CanEdit"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditPlanningBudget)).Succeeded;
        ViewData["CanPublish"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.ManageBudget)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ForecastError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(detail);
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("")]
    public async Task<IActionResult> Create(
        Guid fiscalYearId,
        string name,
        DateOnly asOfDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await forecastService.CreateAsync(fiscalYearId, name, asOfDate, notes, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ForecastError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("{scenarioId:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> UpdateLine(
        Guid scenarioId,
        Guid lineId,
        decimal forecastTotal,
        string? note,
        CancellationToken cancellationToken)
    {
        try
        {
            await forecastService.UpdateLineAsync(scenarioId, lineId, forecastTotal, note, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = scenarioId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ForecastError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id = scenarioId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageBudget)]
    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
        => RunScenarioAction(id, () => forecastService.PublishAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageBudget)]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await forecastService.ArchiveAsync(id, RequireActor(), reason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ForecastError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<IActionResult> RunScenarioAction(Guid id, Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ForecastError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }
}
