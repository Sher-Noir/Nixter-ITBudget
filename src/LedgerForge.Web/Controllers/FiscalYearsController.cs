using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Models.FiscalYears;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageBudget)]
[Route("fiscal-years")]
public sealed class FiscalYearsController(FiscalYearAdministrationService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var years = await service.ListAsync(cancellationToken);
        return View(new FiscalYearIndexViewModel(
            years,
            Saved: Request.Query.ContainsKey("saved")));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        bool isCurrent,
        bool createMonthlyPeriods,
        string initialVersionName,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await service.CreateAsync(
                displayName,
                startDate,
                endDate,
                planningYear,
                description,
                isCurrent,
                createMonthlyPeriods,
                initialVersionName,
                cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            var years = await service.ListAsync(cancellationToken);
            return View("Index", new FiscalYearIndexViewModel(years, exception.Message));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var year = (await service.ListAsync(cancellationToken)).SingleOrDefault(x => x.Id == id);
        if (year is null) return NotFound();

        var versions = await service.ListVersionsAsync(id, cancellationToken);
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(new FiscalYearDetailViewModel(year, versions));
    }

    [HttpPost("{id:guid}/current")]
    public async Task<IActionResult> SetCurrent(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await service.SetCurrentAsync(id, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            TempData["FiscalYearError"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
