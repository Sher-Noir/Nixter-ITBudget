using Crch.ItBudget.Infrastructure.Importing;
using Crch.ItBudget.Web.Models.Imports;
using Crch.ItBudget.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crch.ItBudget.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageImports)]
[Route("imports")]
public sealed class ImportsController(
    Fy2027ImportPreviewService previewService,
    IConfiguration configuration) : Controller
{
    private const long DefaultMaxFileSizeBytes = 25 * 1024 * 1024;

    [HttpGet("")]
    public IActionResult Index() => View(new ImportPreviewViewModel());

    [HttpPost("fy2027/preview")]
    public async Task<IActionResult> Preview(
        IFormFile? workbook,
        CancellationToken cancellationToken)
    {
        if (workbook is null || workbook.Length == 0)
        {
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "Select a non-empty FY2027 .xlsx workbook."));
        }

        var configuredMaxFileSize = configuration.GetValue<long?>("Imports:MaxFileSizeBytes");
        var maxFileSize = configuredMaxFileSize is > 0 ? configuredMaxFileSize.Value : DefaultMaxFileSizeBytes;
        if (workbook.Length > maxFileSize)
        {
            return View("Index", new ImportPreviewViewModel(
                ErrorMessage: $"The workbook exceeds the configured upload limit of {maxFileSize / (1024 * 1024)} MB."));
        }

        if (!string.Equals(Path.GetExtension(workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "Only .xlsx workbooks are accepted for the FY2027 migration."));
        }

        await using var uploaded = new MemoryStream();
        await workbook.CopyToAsync(uploaded, cancellationToken);

        if (!HasZipSignature(uploaded))
        {
            return View("Index", new ImportPreviewViewModel(ErrorMessage: "The uploaded file is not a valid Office Open XML workbook container."));
        }

        uploaded.Position = 0;
        var result = await previewService.CreatePreviewAsync(
            uploaded,
            Path.GetFileName(workbook.FileName),
            cancellationToken);

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

        if (read != signature.Length) return false;

        return signature[0] == 0x50 && signature[1] == 0x4B &&
               ((signature[2] == 0x03 && signature[3] == 0x04) ||
                (signature[2] == 0x05 && signature[3] == 0x06) ||
                (signature[2] == 0x07 && signature[3] == 0x08));
    }
}
