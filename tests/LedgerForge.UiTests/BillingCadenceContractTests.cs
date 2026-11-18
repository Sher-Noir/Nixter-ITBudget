using Xunit;

namespace LedgerForge.UiTests;

public sealed class BillingCadenceContractTests
{
    [Fact]
    public void BillingCadence_CanBeEditedSeparatelyFromRenewalFrequency()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var planning = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var db = Read("src", "LedgerForge.Infrastructure", "Persistence", "LedgerForgeDbContext.cs");
        var migration = Read("src", "LedgerForge.Infrastructure", "Persistence",
            "Migrations", "20261118221500_AddBudgetItemBillingCadence.cs");
        var export = Read("src", "LedgerForge.Web", "Controllers", "ExportsController.cs");
        Assert.Contains("items/@item.Id/billing-cadence", view, StringComparison.Ordinal);
        Assert.Contains("name=\"billingCadence\"", view, StringComparison.Ordinal);
        Assert.Contains("value=\"Monthly\"", view, StringComparison.Ordinal);
        Assert.Contains("value=\"Quarterly\"", view, StringComparison.Ordinal);
        Assert.Contains("value=\"Automatic\"", view, StringComparison.Ordinal);
        Assert.Contains("value=\"OneTime\"", view, StringComparison.Ordinal);
        Assert.Contains("SetBillingCadenceAsync", planning, StringComparison.Ordinal);
        Assert.Contains("HttpPost(\"items/{id:guid}/billing-cadence\")", controller, StringComparison.Ordinal);
        Assert.Contains("ValidateAntiForgeryToken", controller, StringComparison.Ordinal);
        Assert.Contains("HasDefaultValue(BudgetBillingCadence.Automatic)", db, StringComparison.Ordinal);
        Assert.Contains("AddColumn<int>", migration, StringComparison.Ordinal);
        Assert.Contains("Expected Billing Installments To Date", export, StringComparison.Ordinal);
        Assert.Contains("Billing Cadence Basis", export, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailForecast_UsesSameProjectionAsBudgetSummary()
    {
        var detail = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemWorkspaceService.cs");
        Assert.Contains("BudgetItemSpendProjectionQuery.GetAsync(", detail, StringComparison.Ordinal);
        Assert.Contains("projection.FullYearForecast", detail, StringComparison.Ordinal);
        Assert.Contains("SpendProjection", detail, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([dir.FullName, ..segments]));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge root not found");
    }
}
