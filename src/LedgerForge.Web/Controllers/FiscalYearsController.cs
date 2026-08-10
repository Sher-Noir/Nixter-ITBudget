using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Models.FiscalYears;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
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

    [Authorize(Policy = AuthorizationPolicies.ManageFiscalYears)]
    [HttpPost("")]
    public async Task<IActionResult> Create(
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        bool isCurrent,
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
                createMonthlyPeriods: false,
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
        var closeReadiness = await service.GetCloseReadinessAsync(id, cancellationToken);
        ViewData["CanManageFiscalYears"] = (await HttpContext.RequestServices.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, AuthorizationPolicies.ManageFiscalYears)).Succeeded;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(new FiscalYearDetailViewModel(year, versions, closeReadiness));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageFiscalYears)]
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

    [Authorize(Policy = AuthorizationPolicies.ManageFiscalYears)]
    [HttpPost("{id:guid}/close")]
    public Task<IActionResult> CloseYear(Guid id, CancellationToken cancellationToken)
        => RunMutation(id, () => service.CloseAsync(id, RequireActor(), cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.ManageFiscalYears)]
    [HttpPost("{id:guid}/rollover")]
    public async Task<IActionResult> Rollover(
        Guid id,
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        string initialVersionName,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetId = await service.RolloverAsync(
                id,
                displayName,
                startDate,
                endDate,
                planningYear,
                description,
                initialVersionName,
                createMonthlyPeriods: false,
                cancellationToken);
            return RedirectToAction(nameof(Details), new { id = targetId, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["FiscalYearError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<IActionResult> RunMutation(Guid fiscalYearId, Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Details), new { id = fiscalYearId, saved = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["FiscalYearError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id = fiscalYearId });
        }
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor))
            throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }
}