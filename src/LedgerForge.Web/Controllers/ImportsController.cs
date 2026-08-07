using System.Text.Json;
using LedgerForge.Domain.Importing;
using LedgerForge.ImportExport.Spreadsheets;
using LedgerForge.Infrastructure.Importing;
using LedgerForge.Web.Configuration;
using LedgerForge.Web.Models.Imports;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageImports)]
[Route("imports")]
public sealed class ImportsController(
    LegacyBudgetImportPreviewService previewService,
    ImportReviewService reviewService,
    LegacyBudgetImportCommitService commitService,
    IOptions<LegacyImportOptions> legacyImportOptions,
    IConfiguration configuration) : Controller
{
    private const long DefaultMaxFileSizeBytes = 25 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await BuildIndexAsync(TempData["ImportError"] as string, cancellationToken));

    [HttpPost("legacy-budget/preview")]
    public async Task<IActionResult> Preview(IFormFile? workbook, CancellationToken cancellationToken)
    {
        if (workbook is null || workbook.Length == 0)
            return View("Index", await BuildIndexAsync("Select a non-empty .xlsx workbook.", cancellationToken));

        var configuredMaxFileSize = configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
        var maxFileSize = configuredMaxFileSize is > 0 ? configuredMaxFileSize.Value : DefaultMaxFileSizeBytes;
        if (workbook.Length > maxFileSize)
            return View("Index", await BuildIndexAsync($"The workbook exceeds the configured upload limit of {maxFileSize / (1024 * 1024)} MB.", cancellationToken));
        if (!string.Equals(Path.GetExtension(workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return View("Index", await BuildIndexAsync("Only .xlsx workbooks are accepted for this import adapter.", cancellationToken));

        await using var uploaded = new MemoryStream();
        await workbook.CopyToAsync(uploaded, cancellationToken);
        if (!HasZipSignature(uploaded))
            return View("Index", await BuildIndexAsync("The uploaded file is not a valid Office Open XML workbook container.", cancellationToken));

        uploaded.Position = 0;
        var options = legacyImportOptions.Value;
        var hasExpectation = options.ExpectedItemCount is not null || options.ExpectedPlannedTotal is not null || options.ExpectedPriorityNeedLevelCount is not null;
        var expectation = hasExpectation
            ? new ImportReconciliationExpectation(options.ExpectedItemCount, options.ExpectedPlannedTotal, options.PriorityNeedLevel, options.ExpectedPriorityNeedLevelCount)
            : null;

        var result = await previewService.CreatePreviewAsync(uploaded, Path.GetFileName(workbook.FileName), expectation, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = result.ImportBatchId });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await reviewService.GetBatchAsync(id, cancellationToken);
        if (detail is null) return NotFound();

        var targets = !string.IsNullOrWhiteSpace(detail.Batch.AcceptedBy) && detail.Batch.Status == ImportBatchStatus.PreviewReady
            ? await commitService.ListTargetsAsync(cancellationToken)
            : [];

        return View(new ImportBatchDetailViewModel(
            detail,
            targets,
            TempData["ImportReviewError"] as string,
            Request.Query.ContainsKey("saved"),
            Request.Query.ContainsKey("accepted"),
            Request.Query.ContainsKey("committed")));
    }

    [HttpPost("{batchId:guid}/exceptions/{exceptionId:guid}/resolve")]
    public async Task<IActionResult> ResolveException(Guid batchId, Guid exceptionId, string resolutionStatus, string resolutionNote, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ImportExceptionResolutionStatus>(resolutionStatus, false, out var parsedStatus) ||
            !Enum.IsDefined(parsedStatus) || parsedStatus == ImportExceptionResolutionStatus.Open)
        {
            TempData["ImportReviewError"] = "Select a valid exception resolution status.";
            return RedirectToAction(nameof(Details), new { id = batchId });
        }

        return await RunReviewAction(batchId, () => reviewService.ResolveExceptionAsync(
            batchId, exceptionId, parsedStatus, resolutionNote, RequireActor(), cancellationToken));
    }

    [HttpPost("{batchId:guid}/exceptions/{exceptionId:guid}/assign")]
    public Task<IActionResult> AssignException(Guid batchId, Guid exceptionId, string? assignedTo, CancellationToken cancellationToken)
        => RunReviewAction(batchId, () => reviewService.AssignExceptionAsync(batchId, exceptionId, assignedTo, cancellationToken));

    [HttpPost("{batchId:guid}/exceptions/{exceptionId:guid}/reopen")]
    public Task<IActionResult> ReopenException(Guid batchId, Guid exceptionId, CancellationToken cancellationToken)
        => RunReviewAction(batchId, () => reviewService.ReopenExceptionAsync(batchId, exceptionId, cancellationToken));

    [HttpPost("{batchId:guid}/rows/{rowId:guid}/outcome")]
    public async Task<IActionResult> OverrideRowOutcome(
        Guid batchId,
        Guid rowId,
        string outcome,
        string reviewNote,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ImportRowOutcome>(outcome, false, out var parsedOutcome) ||
            parsedOutcome is not (ImportRowOutcome.Accepted or ImportRowOutcome.AcceptedWithWarning or ImportRowOutcome.Rejected))
        {
            TempData["ImportReviewError"] = "Select a valid row disposition.";
            return RedirectToAction(nameof(Details), new { id = batchId });
        }

        return await RunReviewAction(batchId, () => reviewService.OverrideRowOutcomeAsync(
            batchId, rowId, parsedOutcome, RequireActor(), reviewNote, cancellationToken));
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, string? acceptanceReason, CancellationToken cancellationToken)
    {
        try
        {
            await reviewService.AcceptPreviewAsync(id, RequireActor(), acceptanceReason, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, accepted = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ImportReviewError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost("{id:guid}/commit")]
    public async Task<IActionResult> Commit(Guid id, Guid budgetVersionId, CancellationToken cancellationToken)
    {
        try
        {
            await commitService.CommitAsync(id, budgetVersionId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, committed = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            TempData["ImportReviewError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await reviewService.RejectBatchAsync(id, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception)
        {
            TempData["ImportReviewError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<IActionResult> RunReviewAction(Guid batchId, Func<Task> action)
    {
        try
        {
            await action();
            return RedirectToAction(nameof(Details), new { id = batchId, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ImportReviewError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id = batchId });
        }
    }

    private async Task<ImportIndexViewModel> BuildIndexAsync(string? errorMessage, CancellationToken cancellationToken)
        => new(await reviewService.ListBatchesAsync(cancellationToken: cancellationToken), errorMessage);

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor))
            throw new InvalidOperationException("An authenticated directory identity is required for this action.");
        return actor;
    }

    private static bool HasZipSignature(MemoryStream stream)
    {
        if (stream.Length < 4) return false;
        var position = stream.Position;
        stream.Position = 0;
        Span<byte> signature = stackalloc byte[4];
        var read = stream.Read(signature);
        stream.Position = position;
        return read == signature.Length && signature[0] == 0x50 && signature[1] == 0x4B &&
               ((signature[2] == 0x03 && signature[3] == 0x04) ||
                (signature[2] == 0x05 && signature[3] == 0x06) ||
                (signature[2] == 0x07 && signature[3] == 0x08));
    }
}
