using LedgerForge.Web.Updates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize]
[ApiController]
[Route("updates")]
public sealed class UpdatesController(UpdateStatusStore statusStore) : ControllerBase
{
    [HttpGet("status")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status()
    {
        var status = statusStore.Current;
        return Ok(new
        {
            currentVersion = status.CurrentVersion,
            latestVersion = status.LatestVersion,
            isUpdateAvailable = status.IsUpdateAvailable,
            releaseUrl = status.ReleaseUrl,
            checkedAtUtc = status.CheckedAtUtc
        });
    }
}
