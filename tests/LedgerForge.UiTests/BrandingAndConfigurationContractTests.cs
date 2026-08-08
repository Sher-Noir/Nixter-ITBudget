using Xunit;

namespace LedgerForge.UiTests;

public sealed class BrandingAndConfigurationContractTests
{
    [Fact]
    public void BudgetItemBranding_UsesItemLogoThenVendorFallback()
    {
        var service = Read("src", "LedgerForge.Web", "Documents", "BrandingAssetService.cs");
        var itemView = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");

        Assert.Contains("ResolveBudgetItemLogoAsync", service, StringComparison.Ordinal);
        Assert.Contains("BrandingLogoSource.BudgetItem", service, StringComparison.Ordinal);
        Assert.Contains("BrandingLogoSource.Vendor", service, StringComparison.Ordinal);
        Assert.Contains("orderby order.CreatedAtUtc descending", service, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/branding-assets/budget-items/@item.Id/logo", itemView, StringComparison.Ordinal);
        Assert.Contains("Vendor logo fallback", itemView, StringComparison.Ordinal);
        Assert.Contains("Upload item logo", itemView, StringComparison.Ordinal);
    }

    [Fact]
    public void BrandingAssets_AreValidatedAndServedFromProtectedStorage()
    {
        var store = Read("src", "LedgerForge.Web", "Documents", "PhysicalDocumentStore.cs");
        var service = Read("src", "LedgerForge.Web", "Documents", "BrandingAssetService.cs");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BrandingAssetsController.cs");

        Assert.Contains("Display logos must be PNG or JPEG images", service, StringComparison.Ordinal);
        Assert.Contains("MaxLogoFileSizeBytes", service, StringComparison.Ordinal);
        Assert.Contains("Documents:StoragePath", service, StringComparison.Ordinal);
        Assert.Contains("FileMode.Open", service, StringComparison.Ordinal);
        Assert.Contains("FileShare.Read", service, StringComparison.Ordinal);
        Assert.Contains("File content does not match a PNG signature", store, StringComparison.Ordinal);
        Assert.Contains("File content does not match a JPEG signature", store, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.EditPlanningBudget", controller, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.ManageProcurement", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void VendorConfiguration_ExposesLogoEditAndSafeDelete()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Vendors", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "VendorsController.cs");

        Assert.Contains("Vendor logo", view, StringComparison.Ordinal);
        Assert.Contains("Replace logo", view, StringComparison.Ordinal);
        Assert.Contains("Save changes", view, StringComparison.Ordinal);
        Assert.Contains("Delete / Retire", view, StringComparison.Ordinal);
        Assert.Contains("{id:guid}/delete", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedConfiguration_ProvidesSafeDeleteOrRetireActions()
    {
        var deletion = Read("src", "LedgerForge.Infrastructure", "MasterData", "ConfigurationDeletionService.cs");
        var lookupView = Read("src", "LedgerForge.Web", "Views", "AdministrationLookups", "Index.cshtml");
        var financeView = Read("src", "LedgerForge.Web", "Views", "AdministrationFinance", "Index.cshtml");

        Assert.Contains("SqlException { Number: 547 }", deletion, StringComparison.Ordinal);
        Assert.Contains("Deactivate()", deletion, StringComparison.Ordinal);
        Assert.Contains("Delete / Retire", lookupView, StringComparison.Ordinal);
        Assert.Contains("Save changes", lookupView, StringComparison.Ordinal);
        Assert.Contains("Delete / Retire", financeView, StringComparison.Ordinal);
        Assert.Contains("Save changes", financeView, StringComparison.Ordinal);
    }

    [Fact]
    public void SecurityConfiguration_ProvidesEditAndDeleteActions()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "AdministrationSecurity", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "AdministrationSecurityController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Security", "SecurityAdministrationService.cs");

        Assert.Contains("Edit directory mappings", view, StringComparison.Ordinal);
        Assert.Contains("Edit user exceptions", view, StringComparison.Ordinal);
        Assert.Contains("Save changes", view, StringComparison.Ordinal);
        Assert.Contains("class=\"danger\"", view, StringComparison.Ordinal);
        Assert.Contains("DeleteMapping", controller, StringComparison.Ordinal);
        Assert.Contains("DeleteUserException", controller, StringComparison.Ordinal);
        Assert.Contains("UpdateMappingAsync", service, StringComparison.Ordinal);
        Assert.Contains("UpdateUserExceptionAsync", service, StringComparison.Ordinal);
    }

    [Fact]
    public void UnusedPlanningItems_CanBeDeletedWithoutDeletingHistory()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetItemMaintenanceController.cs");

        Assert.Contains("Delete planning item", view, StringComparison.Ordinal);
        Assert.Contains("!item.IsPlanningEditable", controller, StringComparison.Ordinal);
        Assert.Contains("Remove linked documents and the item-specific logo", controller, StringComparison.Ordinal);
        Assert.Contains("SqlException { Number: 547 }", controller, StringComparison.Ordinal);
        Assert.Contains("cannot be deleted", controller, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerForge.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LedgerForge repository root could not be located from the test output directory.");
    }
}
