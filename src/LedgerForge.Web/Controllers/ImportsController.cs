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
    IOptions<LegacyImportOptions> legacyImportOptions,
    IConfiguration configuration) : Controller
{
    private const long DefaultMaxFileSizeBytes = 25 * 1024 * 1024;

    [HttpGet("")]
    public IActionResult Index() => View(new ImportPreviewViewModel());

    [HttpPost("legacy-budget/preview")]
    public async Task<IActionResult> Preview(IFormFile? workbook, CancellationToken cancellationToken)
    {
        if (workbook is null || workbook.Length == 0)
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "Select a non-empty .xlsx workbook."));

        var configuredMaxFileSize = configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
        var maxFileSize = configuredMaxFileSize is > 0 ? configuredMaxFileSize.Value : DefaultMaxFileSizeBytes;
        if (workbook.Length > maxFileSize)
            return View("Index", new ImportPreviewViewModel(ErrorMessage: $"The workbook exceeds the configured upload limit of {maxFileSize / (1024 * 1024)} MB."));

        if (!string.Equals(Path.GetExtension(workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "Only .xlsx workbooks are accepted for this import adapter."));

        await using var uploaded = new MemoryStream();
        await workbook.CopyToAsync(uploaded, cancellationToken);
        if (!HasZipSignature(uploaded))
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "The uploaded file is not a valid Office Open XML workbook container."));

        uploaded.Position = 0;
        var options = legacyImportOptions.Value;
        var hasExpectation = options.ExpectedItemCount is not null || options.ExpectedPlannedTotal is not null || options.ExpectedPriorityNeedLevelCount is not null;
        var expectation = hasExpectation
            ? new ImportReconciliationExpectation(options.ExpectedItemCount, options.ExpectedPlannedTotal, options.PriorityNeedLevel, options.ExpectedPriorityNeedLevelCount)
            : null;

        var result = await previewService.CreatePreviewAsync(uploaded, Path.GetFileName(workbook.FileName), expectation, cancellationToken);
        return View("Index", new ImportPreviewViewModel(Result: result));
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
