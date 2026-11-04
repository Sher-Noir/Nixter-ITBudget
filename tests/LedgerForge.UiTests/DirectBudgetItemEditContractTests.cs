using Xunit;

namespace LedgerForge.UiTests;

public sealed class DirectBudgetItemEditContractTests
{
    [Fact]
    public void OrganizationPolicy_IsConfigurableInAdminAndDefaultsToDirectEditing()
    {
        var options = Read("src", "LedgerForge.Web", "Configuration", "BrandingOptions.cs");
        var store = Read("src", "LedgerForge.Web", "Configuration", "OrganizationSettingsStore.cs");
        var admin = Read("src", "LedgerForge.Web", "Views", "AdministrationOrganization", "Index.cshtml");

        Assert.Contains("RequireReapprovalForBudgetItemEdits { get; set; } = false", options, StringComparison.Ordinal);
        Assert.Contains("RequireReapprovalForBudgetItemEdits = value.RequireReapprovalForBudgetItemEdits", store, StringComparison.Ordinal);
        Assert.Contains("RequireReapprovalForBudgetItemEdits = source.RequireReapprovalForBudgetItemEdits", store, StringComparison.Ordinal);
        Assert.Contains("name=\"RequireReapprovalForBudgetItemEdits\"", admin, StringComparison.Ordinal);
        Assert.Contains("Allow direct edits without resubmitting for approval", admin, StringComparison.Ordinal);
        Assert.Contains("Require reapproval when editing approved items", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedActivePurchased_CanSaveWithoutReopeningOnlyWhenOrgPolicyAllows()
    {
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var domain = Read("src", "LedgerForge.Domain", "Budgeting", "BudgetItem.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");

        Assert.Contains("organizationSettingsStore.GetAsync(cancellationToken)", controller, StringComparison.Ordinal);
        Assert.Contains("snapshot.Status is BudgetItemStatus.Approved or BudgetItemStatus.Active or BudgetItemStatus.Purchased", controller, StringComparison.Ordinal);
        Assert.Contains("allowDirectApprovedEdits: allowDirectApprovedEdits", controller, StringComparison.Ordinal);
        Assert.Contains("item.ChangeCost(quantity, unitCost, allowDirectApprovedEdits)", service, StringComparison.Ordinal);
        Assert.Contains("item.UpdateVendorAndNotes(vendorId, notes, allowDirectApprovedEdits)", service, StringComparison.Ordinal);
        Assert.Contains("RevisedTotal = newTotal", domain, StringComparison.Ordinal);
        Assert.Contains("directEditWithoutReapproval", view, StringComparison.Ordinal);
        Assert.Contains("Save updates without resubmitting", view, StringComparison.Ordinal);
        Assert.Contains("item.Status is BudgetItemStatus.Draft or BudgetItemStatus.Proposed or BudgetItemStatus.Deferred", view, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([directory.FullName, ..segments]));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge repository root could not be located.");
    }
}
