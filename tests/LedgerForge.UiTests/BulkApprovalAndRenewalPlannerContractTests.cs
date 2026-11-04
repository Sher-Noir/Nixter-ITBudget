using Xunit;

namespace LedgerForge.UiTests;

public sealed class BulkApprovalAndRenewalPlannerContractTests
{
    [Fact]
    public void BudgetBulkSubmit_SelectionIsScopedToVisiblePage_AndServerValidatesEveryId()
    {
        var budgetView = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Approvals", "ApprovalQueueService.cs");
        var script = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-ui.js");

        Assert.Contains("form=\"budget-bulk-submit\"", budgetView, StringComparison.Ordinal);
        Assert.Contains("data-bulk-select-all=\"budget-bulk-submit\"", budgetView, StringComparison.Ordinal);
        Assert.Contains("@foreach (var item in table.Rows)", budgetView, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmSelection\"", budgetView, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"bulk-submit\")]", controller, StringComparison.Ordinal);
        Assert.Contains("SubmitBudgetItemsAsync(", controller, StringComparison.Ordinal);
        Assert.Contains("SubmitBudgetItemsAsync(", service, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync(cancellationToken)", service, StringComparison.Ordinal);
        Assert.Contains("itemIds.Distinct().Count()", service, StringComparison.Ordinal);
        Assert.Contains("items.Any(x => x.IsArchived || !x.IsPlanningEditable)", service, StringComparison.Ordinal);
        Assert.Contains("[data-bulk-select-all]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingQueue_BulkApproveAppliesOnlyToSelectedSubmittedBudgetItems()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Approvals", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "ApprovalsController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Approvals", "ApprovalQueueService.cs");

        Assert.Contains("form=\"bulk-budget-approve\"", view, StringComparison.Ordinal);
        Assert.Contains("Approve selected budget items", view, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"budget-items/bulk-approve\")]", controller, StringComparison.Ordinal);
        Assert.Contains("ApproveBudgetItemsAsync(", service, StringComparison.Ordinal);
        Assert.Contains("x.Status != BudgetItemStatus.Submitted", service, StringComparison.Ordinal);
        Assert.Contains("item.Approve(actor, now, note)", service, StringComparison.Ordinal);
        Assert.Contains("transaction.CommitAsync(cancellationToken)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void NextYearPlanner_ForecastsAndCarriesDraftsWithoutRequiringYearClose()
    {
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "NextFiscalYearRenewalPlanningService.cs");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "RenewalsController.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "Renewals", "Plan.cshtml");
        var calendar = Read("src", "LedgerForge.Web", "Views", "Renewals", "Index.cshtml");
        var startup = Read("src", "LedgerForge.Web", "Program.cs");

        Assert.Contains("NextFiscalYearRenewalPlanningService", startup, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"plan\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"plan\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"plan/create-fiscal-year\")]", controller, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.ManageFiscalYears", controller, StringComparison.Ordinal);
        Assert.Contains("Create next fiscal year and open planner", view, StringComparison.Ordinal);
        Assert.Contains("plan/export.csv", controller, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-next-fy-renewal-forecast.csv", controller, StringComparison.Ordinal);
        Assert.Contains("Export forecast CSV", view, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.EditPlanningBudget", controller, StringComparison.Ordinal);
        Assert.Contains("href=\"/renewals/plan\"", calendar, StringComparison.Ordinal);
        Assert.Contains("data-bulk-select-all=\"renewal-carry-form\"", view, StringComparison.Ordinal);
        Assert.Contains("Estimated price change", view, StringComparison.Ordinal);
        Assert.Contains("Already planned", view, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("candidate.AlreadyPlanned", service, StringComparison.Ordinal);
        Assert.Contains("PurchaseType.Renewal", service, StringComparison.Ordinal);
        Assert.Contains("dbContext.BudgetItems.Add(targetItem)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceYear.Status != FiscalYearStatus.Closed", service, StringComparison.Ordinal);
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
