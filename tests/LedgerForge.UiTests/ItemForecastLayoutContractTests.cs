using Xunit;

namespace LedgerForge.UiTests;

public sealed class ItemForecastLayoutContractTests
{
    [Fact]
    public void ItemView_ShowsForecastBreakdownAndRemovesDuplicatedDocumentsCard()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-item.css");
        var js = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-budget.js");
        var svc = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemWorkspaceService.cs");

        Assert.Contains("data-open-forecast-breakdown", view, StringComparison.Ordinal);
        Assert.Contains("id=\"forecast-breakdown\"", view, StringComparison.Ordinal);
        Assert.Contains("ForecastDetails.PostedActual", view, StringComparison.Ordinal);
        Assert.Contains("ForecastDetails.UnpostedRecurring", view, StringComparison.Ordinal);
        Assert.Contains("ForecastDetails.PublishedScenarioTotal", view, StringComparison.Ordinal);
        Assert.Contains("BudgetItemForecastCalculator.Calculate(", svc, StringComparison.Ordinal);
        Assert.Contains("RecurringActualOccurrences", svc, StringComparison.Ordinal);
        Assert.Contains("data-open-forecast-breakdown", js, StringComparison.Ordinal);
        Assert.DoesNotContain("<section id=\"documents\" class=\"lf-card\">", view, StringComparison.Ordinal);
        Assert.Contains("href=\"@documentUploadUrl\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"purchase-orders\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"recurring-actuals\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"actuals-invoices\"", view, StringComparison.Ordinal);
        Assert.Contains(":is(#purchase-orders, #recurring-actuals, #actuals-invoices)", css, StringComparison.Ordinal);
        Assert.Contains("height: 440px;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", css, StringComparison.Ordinal);
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
