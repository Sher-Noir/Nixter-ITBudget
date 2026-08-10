using LedgerForge.Web.Documents;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("branding-assets")]
public sealed class BrandingAssetsController(BrandingAssetService brandingAssetService) : Controller
{
    [HttpGet("organization/logo")]
    public async Task<IActionResult> OrganizationLogo(CancellationToken cancellationToken)
    {
        var logo = await brandingAssetService.GetOrganizationLogoAsync(cancellationToken);
        return logo is null ? NotFound() : OpenLogo(logo);
    }

    [HttpGet("organization/icon")]
    public async Task<IActionResult> OrganizationIcon(CancellationToken cancellationToken)
    {
        var icon = await brandingAssetService.GetOrganizationIconAsync(cancellationToken);
        return icon is null ? NotFound() : OpenLogo(icon);
    }

    [HttpGet("budget-items/{id:guid}/logo")]
    public async Task<IActionResult> BudgetItemLogo(Guid id, CancellationToken cancellationToken)
    {
        var logo = await brandingAssetService.ResolveBudgetItemLogoAsync(id, cancellationToken);
        return logo is null ? NotFound() : OpenLogo(logo);
    }

    [HttpGet("vendors/{id:guid}/logo")]
    public async Task<IActionResult> VendorLogo(Guid id, CancellationToken cancellationToken)
    {
        var logo = await brandingAssetService.GetVendorLogoAsync(id, cancellationToken);
        return logo is null ? NotFound() : OpenLogo(logo);
    }

    [Authorize(Policy = AuthorizationPolicies.Administration)]
    [HttpPost("organization/logo")]
    [RequestFormLimits(MultipartBodyLengthLimit = BrandingAssetService.MaxLogoFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadOrganizationLogo(IFormFile? logo, CancellationToken cancellationToken)
    {
        if (logo is null || logo.Length == 0)
            return RedirectToOrganizationError("Select a PNG or JPEG organization logo to upload.");
        if (logo.Length > BrandingAssetService.MaxLogoFileSizeBytes)
            return RedirectToOrganizationError("Organization logo cannot exceed 5 MB.");

        try
        {
            await using var source = logo.OpenReadStream();
            await brandingAssetService.SaveOrganizationLogoAsync(source, Path.GetFileName(logo.FileName), RequireActor(), cancellationToken);
            return Redirect("/admin/organization?brandingSaved=true#branding-assets");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            return RedirectToOrganizationError(exception.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.Administration)]
    [HttpPost("organization/logo/remove")]
    public async Task<IActionResult> RemoveOrganizationLogo(CancellationToken cancellationToken)
    {
        await brandingAssetService.RemoveOrganizationLogoAsync(cancellationToken);
        return Redirect("/admin/organization?brandingSaved=true#branding-assets");
    }

    [Authorize(Policy = AuthorizationPolicies.Administration)]
    [HttpPost("organization/icon")]
    [RequestFormLimits(MultipartBodyLengthLimit = BrandingAssetService.MaxLogoFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadOrganizationIcon(IFormFile? icon, CancellationToken cancellationToken)
    {
        if (icon is null || icon.Length == 0)
            return RedirectToOrganizationError("Select a PNG or JPEG browser icon to upload.");
        if (icon.Length > BrandingAssetService.MaxLogoFileSizeBytes)
            return RedirectToOrganizationError("Browser icon cannot exceed 5 MB.");

        try
        {
            await using var source = icon.OpenReadStream();
            await brandingAssetService.SaveOrganizationIconAsync(source, Path.GetFileName(icon.FileName), RequireActor(), cancellationToken);
            return Redirect("/admin/organization?brandingSaved=true#branding-assets");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            return RedirectToOrganizationError(exception.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.Administration)]
    [HttpPost("organization/icon/remove")]
    public async Task<IActionResult> RemoveOrganizationIcon(CancellationToken cancellationToken)
    {
        await brandingAssetService.RemoveOrganizationIconAsync(cancellationToken);
        return Redirect("/admin/organization?brandingSaved=true#branding-assets");
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("budget-items/{id:guid}/logo")]
    [RequestFormLimits(MultipartBodyLengthLimit = BrandingAssetService.MaxLogoFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadBudgetItemLogo(Guid id, IFormFile? logo, CancellationToken cancellationToken)
    {
        if (logo is null || logo.Length == 0)
            return RedirectToBudgetItemError(id, "Select a PNG or JPEG logo to upload.");
        if (logo.Length > BrandingAssetService.MaxLogoFileSizeBytes)
            return RedirectToBudgetItemError(id, "Display logo cannot exceed 5 MB.");

        try
        {
            await using var source = logo.OpenReadStream();
            await brandingAssetService.SaveBudgetItemLogoAsync(id, source, Path.GetFileName(logo.FileName), RequireActor(), cancellationToken);
            return Redirect($"/budget/items/{id}?saved=true#item-branding");
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            return RedirectToBudgetItemError(id, exception.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.EditPlanningBudget)]
    [HttpPost("budget-items/{id:guid}/logo/remove")]
    public async Task<IActionResult> RemoveBudgetItemLogo(Guid id, CancellationToken cancellationToken)
    {
        await brandingAssetService.RemoveBudgetItemLogoAsync(id, cancellationToken);
        return Redirect($"/budget/items/{id}?saved=true#item-branding");
    }

    [Authorize(Policy = AuthorizationPolicies.ManageVendors)]
    [HttpPost("vendors/{id:guid}/logo")]
    [RequestFormLimits(MultipartBodyLengthLimit = BrandingAssetService.MaxLogoFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadVendorLogo(Guid id, IFormFile? logo, CancellationToken cancellationToken)
    {
        if (logo is null || logo.Length == 0)
            return RedirectToVendorError("Select a PNG or JPEG logo to upload.");
        if (logo.Length > BrandingAssetService.MaxLogoFileSizeBytes)
            return RedirectToVendorError("Display logo cannot exceed 5 MB.");

        try
        {
            await using var source = logo.OpenReadStream();
            await brandingAssetService.SaveVendorLogoAsync(id, source, Path.GetFileName(logo.FileName), RequireActor(), cancellationToken);
            return Redirect("/vendors?saved=true");
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException)
        {
            return RedirectToVendorError(exception.Message);
        }
    }

    [Authorize(Policy = AuthorizationPolicies.ManageVendors)]
    [HttpPost("vendors/{id:guid}/logo/remove")]
    public async Task<IActionResult> RemoveVendorLogo(Guid id, CancellationToken cancellationToken)
    {
        await brandingAssetService.RemoveVendorLogoAsync(id, cancellationToken);
        return Redirect("/vendors?saved=true");
    }

    private IActionResult OpenLogo(BrandingLogoReference logo)
    {
        try
        {
            Response.Headers.CacheControl = "private, no-cache, no-store";
            return File(brandingAssetService.OpenRead(logo), logo.ContentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private IActionResult RedirectToBudgetItemError(Guid id, string message)
    {
        TempData["BudgetItemError"] = message;
        return Redirect($"/budget/items/{id}#item-branding");
    }

    private IActionResult RedirectToVendorError(string message)
    {
        TempData["VendorError"] = message;
        return Redirect("/vendors");
    }

    private IActionResult RedirectToOrganizationError(string message)
    {
        TempData["OrganizationBrandingError"] = message;
        return Redirect("/admin/organization#branding-assets");
    }

    private string RequireActor()
    {
        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("An authenticated directory identity is required for branding changes.");
        return actor;
    }
}