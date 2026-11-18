using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetSummaryKpiContractTests
{
    [Fact]
    public void BudgetView_HasExactlyTheFiveRequestedMetricsAndSignedVariance()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-workspace.css");
        var planning = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var projection = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemSpendProjectionQuery.cs");
        var section = view.Split("<section class=\"lf-budget-kpis\"", 2)[1].Split("</section>", 2)[0];

        Assert.Equal(5, section.Split("<article class=\"lf-card lf-budget-kpi", StringSplitOptions.None).Length - 1);
        Assert.Contains("Total Items", section, StringComparison.Ordinal);
        Assert.Contains("Planned Total", section, StringComparison.Ordinal);
        Assert.Contains("Current Actual", section, StringComparison.Ordinal);
        Assert.Contains("Current Forecasted", section, StringComparison.Ordinal);
        Assert.Contains("Current Over/Under", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Must Have", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Open / Proposed", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved", section, StringComparison.Ordinal);
        Assert.Contains("expectedToDate - displayedActual", view, StringComparison.Ordinal);
        Assert.Contains("\"-\" + Math.Abs(currentOverUnder)", view, StringComparison.Ordinal);
        Assert.Contains("Expected to date: @expectedToDate", view, StringComparison.Ordinal);
        Assert.Contains("FullYearForecast", planning, StringComparison.Ordinal);
        Assert.Contains("ExpectedToDate", planning, StringComparison.Ordinal);
        Assert.Contains("PostedMonth", projection, StringComparison.Ordinal);
        Assert.Contains("BudgetItemForecastCalculator.Calculate", projection, StringComparison.Ordinal);
        Assert.Contains(".lf-budget-over-under.is-over", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemAndBudgetButtons_HaveUniformHeightsAndDontStretch()
    {
        var itemCss = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-item.css");
        var workspace = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-workspace.css");
        Assert.Contains(".lf-item-hero-actions > form > .button", itemCss, StringComparison.Ordinal);
        Assert.Contains("height: 40px", itemCss, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap", itemCss, StringComparison.Ordinal);
        Assert.Contains(".lf-budget-action-buttons > .button", workspace, StringComparison.Ordinal);
        Assert.Contains("flex: 0 0 auto;", workspace, StringComparison.Ordinal);
        Assert.Contains("width: auto;", workspace, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([current.FullName, ..segments]));
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge repository root not located.");
    }
}
