using System.Text.Json;
using LedgerForge.Domain.Auditing;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Web.Documents;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("documents")]
public sealed class DocumentsController : Controller
{
    private readonly PhysicalDocumentStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly LedgerForgeDbContext _dbContext;

    public DocumentsController(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IAuthorizationService authorizationService,
        LedgerForgeDbContext dbContext)
    {
        var configuredLimit = configuration.GetValue<long?>("Documents:MaxFileSizeBytes");
        var configuredStoragePath = configuration.GetValue<string>("Documents:StoragePath");
        _store = new PhysicalDocumentStore(
            environment.ContentRootPath,
            configuredStoragePath,
            configuredLimit is > 0 ? configuredLimit.Value : 25L * 1024 * 1024);
        _authorizationService = authorizationService;
        _dbContext = dbContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CanManageDocuments"] = await CanEditDocumentsAsync();
        ViewData["ErrorMessage"] = TempData["DocumentError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(await _store.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var document = await _store.GetAsync(id, cancellationToken);
        if (document is null) return NotFound();
        ViewData["CanManageDocuments"] = await CanEditDocumentsAsync();
        ViewData["ErrorMessage"] = TempData["DocumentError"] as string;
        ViewData["Saved"] = Request.Query.ContainsKey("saved");
        return View(document);
    }

    [Authorize(Policy = AuthorizationPolicies.EditDocuments)]
    [HttpPost("")]
    [RequestFormLimits(MultipartBodyLengthLimit = 26L * 1024 * 1024)]
    public async Task<IActionResult> Create(
        string title,
        string? description,
        string? linkedEntityType,
        Guid? linkedEntityId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["DocumentError"] = "Select a non-empty document to upload.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using var source = file.OpenReadStream();
            var document = await _store.CreateAsync(
                title,
                description,
                linkedEntityType,
                linkedEntityId,
                source,
                Path.GetFileName(file.FileName),
                RequireActor(),
                cancellationToken);
            await TryRecordAuditAsync(
                "Document",
                document.Id,
                new { document.Title, document.Description, document.LinkedEntityType, document.LinkedEntityId, Version = 1 },
                cancellationToken);
            return RedirectToAction(nameof(Details), new { id = document.Id, saved = true });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            TempData["DocumentError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditDocuments)]
    [HttpPost("{id:guid}/versions")]
    [RequestFormLimits(MultipartBodyLengthLimit = 26L * 1024 * 1024)]
    public async Task<IActionResult> AddVersion(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["DocumentError"] = "Select a non-empty document version to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await using var source = file.OpenReadStream();
            var document = await _store.AddVersionAsync(id, source, Path.GetFileName(file.FileName), RequireActor(), cancellationToken);
            var version = document.Versions.MaxBy(x => x.VersionNumber)!;
            await TryRecordAuditAsync("Document", id, new { AddedVersion = version.VersionNumber, version.OriginalFileName, version.Sha256 }, cancellationToken);
            return RedirectToAction(nameof(Details), new { id, saved = true });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            TempData["DocumentError"] = exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _store.OpenDownloadAsync(
                id,
                versionId,
                RequireActor(),
                HttpContext.TraceIdentifier,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
            await TryRecordAuditAsync(
                "Document",
                id,
                new { DownloadedVersionId = versionId, download.Version.VersionNumber, download.Version.OriginalFileName },
                cancellationToken);
            return File(download.Stream, download.Version.ContentType, download.Version.OriginalFileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (FileNotFoundException) { return NotFound(); }
    }

    private async Task<bool> CanEditDocumentsAsync()
        => (await _authorizationService.AuthorizeAsync(User, AuthorizationPolicies.EditDocuments)).Succeeded;

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("An authenticated directory identity is required for document access.");
        return actor;
    }

    private async Task TryRecordAuditAsync(string entityType, Guid entityId, object after, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.AuditEvents.Add(new AuditEvent(
                RequireActor(),
                entityType,
                entityId,
                AuditAction.Explicit,
                null,
                JsonSerializer.Serialize(after),
                HttpContext.TraceIdentifier,
                DateTimeOffset.UtcNow,
                Request.Method,
                Request.Path,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Document metadata and local access logs remain authoritative if central audit persistence is temporarily unavailable.
        }
    }
}