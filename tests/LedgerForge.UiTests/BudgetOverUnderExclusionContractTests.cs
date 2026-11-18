using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetOverUnderExclusionContractTests
{
    [Fact]
    public void BudgetTableAndItemDetail_ExposePersistentCheckboxWithoutAffectingOtherKpis()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var detail = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-budget.css");
        var js = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-budget.js");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var entity = Read("src", "LedgerForge.Domain", "Budgeting", "BudgetItem.cs");
        var migration = Read("src", "LedgerForge.Infrastructure", "Persistence",
            "Migrations", "20261118233000_AddBudgetItemOverUnderExclusion.cs");

        Assert.Contains("name=\"excludeFromOverUnder\"", view, StringComparison.Ordinal);
        Assert.Contains("data-budget-exclusion-toggle", view, StringComparison.Ordinal);
        Assert.Contains("name=\"excludeFromOverUnder\"", detail, StringComparison.Ordinal);
        Assert.Contains("Save exclusion", detail, StringComparison.Ordinal);
        Assert.Contains("HttpPost(\"items/{id:guid}/over-under-exclusion\")", controller, StringComparison.Ordinal);
        Assert.Contains("ValidateAntiForgeryToken", controller, StringComparison.Ordinal);
        Assert.Contains("Url.IsLocalUrl(returnUrl)", controller, StringComparison.Ordinal);
        Assert.Contains("SetOverUnderExclusionAsync", service, StringComparison.Ordinal);
        Assert.Contains("SetOverUnderExclusion", entity, StringComparison.Ordinal);
        Assert.Contains("AddColumn<bool>", migration, StringComparison.Ordinal);
        Assert.Contains("defaultValue: false", migration, StringComparison.Ordinal);
        Assert.Contains(".lf-budget-variance-toggle", css, StringComparison.Ordinal);
        Assert.Contains("form.requestSubmit()", js, StringComparison.Ordinal);

        Assert.Contains("var displayedActual = filteredItems.Sum(x => x.Actual)", view, StringComparison.Ordinal);
        Assert.Contains("var displayedForecast = filteredItems.Sum(x => x.Forecast)", view, StringComparison.Ordinal);
        Assert.Contains("BudgetOverUnderCalculator.Calculate(", view, StringComparison.Ordinal);
        Assert.Contains("excludedExpected", view, StringComparison.Ordinal);
        Assert.Contains("excludedActual", view, StringComparison.Ordinal);
        Assert.Contains("actualForVariance", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_IdentifiesExcludedAndRetainsGrossAmountForTraceability()
    {
        var export = Read("src", "LedgerForge.Web", "Controllers", "ExportsController.cs");
        var source = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemSpendProjectionQuery.cs");
        Assert.Contains("Excluded From Over Under", export, StringComparison.Ordinal);
        Assert.Contains("Actual Included In Over Under", export, StringComparison.Ordinal);
        Assert.Contains("Actual Excluded From Over Under", export, StringComparison.Ordinal);
        Assert.Contains("Expected To Date Before Exclusion", export, StringComparison.Ordinal);
        Assert.Contains("pace.ExpectedIncluded", export, StringComparison.Ordinal);
        Assert.Contains("pace.Variance", export, StringComparison.Ordinal);
        Assert.Contains("x.Actual, x.Forecast", export, StringComparison.Ordinal);
        Assert.Contains("One-time: estimated purchase date only; not purchased or billed", source, StringComparison.Ordinal);
    }

    private static string Read(params string[] path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([dir.FullName, ..path]));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge repository root not found.");
    }
}
