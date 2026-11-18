using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetDetailedExportContractTests
{
    [Fact]
    public void Export_ContainsEveryKpiInputAndCalculatesSignedOverUnder()
    {
        var export = Read("src", "LedgerForge.Web", "Controllers", "ExportsController.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var planning = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var projection = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemSpendProjectionQuery.cs");
        var filters = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemFilters.cs");

        // All original columns remain for existing Excel workflows.
        foreach (var title in new[] {
            "Fiscal Year", "Budget Version", "Item Number", "Description", "Status",
            "Quantity", "Unit Cost", "Planned Total", "Approved Total", "Revised Total",
            "Estimated Purchase Date", "Renewal Date"
        })
            Assert.Contains($"\"{title}\"", export, StringComparison.Ordinal);

        foreach (var name in new[] {
            "Current Actual", "Current Forecasted (Full Year)", "Expected To Date",
            "Current Over Under (Expected Minus Actual)", "Expected Recurring To Date",
            "Expected Dated Purchases To Date",
            "Expected Undated Spent Or Purchased To Date",
            "Expected Undated Fiscal Pacing To Date",
            "Expected To Date Basis", "Remaining Unposted Recurring",
            "Outstanding PO Commitments", "Additional PO Commitments Beyond Recurring",
            "Published Forecast Scenario Total", "Effective Forecast Floor",
            "Additional Expected From Forecast Floor", "Recurring Schedules",
            "As Of Date (Eastern)", "Fiscal Year Start", "Fiscal Year End"
        })
            Assert.Contains(name, export, StringComparison.Ordinal);

        Assert.Contains("x.ExpectedToDate - x.Actual", export, StringComparison.Ordinal);
        Assert.Contains("TOTAL ({items.Length} filtered items)", export, StringComparison.Ordinal);
        Assert.Contains("totals[index] = itemRows.Sum", export, StringComparison.Ordinal);
        Assert.Contains("GetSnapshotAsync(", export, StringComparison.Ordinal);
        Assert.Contains("BudgetItemFilters.Apply(snapshot.Items", export, StringComparison.Ordinal);
        Assert.Contains("BudgetItemFilters.Apply(snapshot.Items", view, StringComparison.Ordinal);
        Assert.Contains("BudgetItemFilterOptions", filters, StringComparison.Ordinal);
        Assert.Contains("projections.GetValueOrDefault(x.Id)?.UnpostedRecurring", planning, StringComparison.Ordinal);
        Assert.Contains("RecurringScheduleDetails", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportLink_PassesSelectedVersionAndAllDashboardFilters()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var start = view.IndexOf("Url.Action(\"Budget\", \"Exports\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = view.IndexOf("})", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var url = view[start..end];
        foreach (var parameter in new[] {
            "fiscalYearId = snapshot.SelectedFiscalYearId",
            "versionId = snapshot.SelectedVersionId",
            "section = sectionFilter", "category = categoryFilter",
            "location = locationFilter", "vendor = vendorFilter",
            "need = needFilter", "status = statusFilter", "q = searchFilter"
        })
            Assert.Contains(parameter, url, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([d.FullName, ..segments]));
            d = d.Parent;
        }
        throw new DirectoryNotFoundException("Could not find LedgerForge source tree.");
    }
}
