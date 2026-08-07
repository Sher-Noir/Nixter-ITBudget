using System.Text;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("actuals")]
public sealed class ActualsController(
    ActualLedgerService actualLedgerService,
    ActualCsvImportService actualCsvImportService,
    IAuthorizationService authorizationService,
    IConfiguration configuration) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? fiscalYearId, CancellationToken cancellationToken)
    {
        ViewData["CanPostActuals"] = (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.PostActuals)).Succeeded;
        ViewData["ErrorMessage"] = TempData["ActualsError"] as string;
        ViewData["SuccessMessage"] = TempData["ActualsSuccess"] as string;
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
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["ActualsError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PostActuals)]
    [HttpPost("import")]
    [RequestFormLimits(MultipartBodyLengthLimit = 26L * 1024 * 1024)]
    public async Task<IActionResult> Import(Guid fiscalYearId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ActualsError"] = "Select a non-empty CSV file to import.";
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ActualsError"] = "Actuals import accepts CSV files only.";
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await actualCsvImportService.ImportAsync(
                fiscalYearId,
                stream,
                BuildImportProfile(),
                Path.GetFileName(file.FileName),
                cancellationToken);
            if (!result.Succeeded)
            {
                var shown = result.Errors.Take(10).ToArray();
                var suffix = result.Errors.Count > shown.Length ? $" (+{result.Errors.Count - shown.Length} more error(s))" : string.Empty;
                TempData["ActualsError"] = "Import rejected; no rows were posted. " + string.Join(" ", shown) + suffix;
                return RedirectToAction(nameof(Index), new { fiscalYearId });
            }

            TempData["ActualsSuccess"] = $"Imported {result.ImportedRows:N0} actual row(s) totaling {result.ImportedTotal:C2}.";
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or IOException or DecoderFallbackException or OverflowException)
        {
            TempData["ActualsError"] = $"Import failed; no rows were posted. {exception.Message}";
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
    }

    [Authorize(Policy = AuthorizationPolicies.PostActuals)]
    [HttpGet("import-template.csv")]
    public IActionResult ImportTemplate()
    {
        var profile = BuildImportProfile();
        var header = string.Join(',', new[]
        {
            profile.TransactionDateHeader, profile.AmountHeader, profile.DescriptionHeader,
            profile.SourceReferenceHeader, profile.BudgetItemHeader, profile.FinanceAccountHeader,
            profile.DepartmentHeader, profile.LocationHeader, profile.FiscalPeriodHeader
        }.Select(Csv));
        var sample = string.Join(',', new[] { "2027-01-15", "1250.00", "Example imported transaction", "JRN-1001", "", "", "", "", "" }.Select(Csv));
        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(header + "\r\n" + sample + "\r\n"), "text/csv; charset=utf-8", "ledgerforge-actuals-import-template.csv");
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
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            TempData["ActualsError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { fiscalYearId });
        }
    }

    private ActualImportProfile BuildImportProfile()
    {
        string Header(string key, string fallback) => configuration[$"ActualsImport:Headers:{key}"]?.Trim() is { Length: > 0 } value ? value : fallback;
        return new(
            Header("TransactionDate", ActualImportProfile.Default.TransactionDateHeader),
            Header("Amount", ActualImportProfile.Default.AmountHeader),
            Header("Description", ActualImportProfile.Default.DescriptionHeader),
            Header("SourceReference", ActualImportProfile.Default.SourceReferenceHeader),
            Header("BudgetItem", ActualImportProfile.Default.BudgetItemHeader),
            Header("FinanceAccount", ActualImportProfile.Default.FinanceAccountHeader),
            Header("Department", ActualImportProfile.Default.DepartmentHeader),
            Header("Location", ActualImportProfile.Default.LocationHeader),
            Header("FiscalPeriod", ActualImportProfile.Default.FiscalPeriodHeader));
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
