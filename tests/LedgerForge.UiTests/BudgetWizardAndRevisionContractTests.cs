using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetWizardAndRevisionContractTests
{
    [Fact]
    public void NewItemWizard_GeneratesNumberAndOnlySubmitsOnFinalStep()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var script = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-budget.js");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-budget.css");

        Assert.DoesNotContain("id=\"create-itemNumber\"", view, StringComparison.Ordinal);
        Assert.Contains("CreateDetailedItemAsync(", controller, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("existingNumbers", service, StringComparison.Ordinal);
        Assert.Contains("next.hidden = step === steps.length - 1", script, StringComparison.Ordinal);
        Assert.Contains("save.hidden = step !== steps.length - 1", script, StringComparison.Ordinal);
        Assert.Contains("if (step !== steps.length - 1)", script, StringComparison.Ordinal);
        Assert.Contains("#lf-budget-create-form [hidden]", css, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetItemSelections_AreSearchableAndKeepPostedSelectFields()
    {
        var script = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-budget.js");
        Assert.Contains("const enhanceSelect = select =>", script, StringComparison.Ordinal);
        Assert.Contains("select.dispatchEvent(new Event(\"change\"", script, StringComparison.Ordinal);
        Assert.Contains("#lf-budget-create-form select, #edit-item select", script, StringComparison.Ordinal);
        Assert.Contains("aria-activedescendant", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SubmittedAndApprovedItems_ExposeAuditedReopenAction()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Approvals", "ApprovalQueueService.cs");

        Assert.Contains("/budget/items/@item.Id/reopen", view, StringComparison.Ordinal);
        Assert.Contains("ReopenBudgetItemForRevisionAsync", controller, StringComparison.Ordinal);
        Assert.Contains("version.IsLocked", service, StringComparison.Ordinal);
        Assert.Contains("AuditAction.Explicit", service, StringComparison.Ordinal);
    }

    [Fact]
    public void NewBudgetItem_ReturnsToBudgetListing_NotItemDetail()
    {
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var createStart = controller.IndexOf("[HttpPost(\"create\")]", StringComparison.Ordinal);
        var quickAddStart = controller.IndexOf("[HttpPost(\"quick-add\")]", StringComparison.Ordinal);
        Assert.True(createStart >= 0 && quickAddStart > createStart);
        var createHandler = controller[createStart..quickAddStart];
        Assert.Contains("RedirectToAction(nameof(Index), new { fiscalYearId, versionId, saved = true })", createHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("RedirectToAction(nameof(Item)", createHandler, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetTable_SortLinksPagingAndFiltersStayTogether()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-budget.css");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemTable.cs");

        Assert.Contains("BudgetItemTable.Create(", view, StringComparison.Ordinal);
        Assert.Contains("@foreach (var item in table.Rows)", view, StringComparison.Ordinal);
        Assert.Contains("aria-sort=\"@SortAria(", view, StringComparison.Ordinal);
        Assert.Contains("lf-budget-pagination", view, StringComparison.Ordinal);
        Assert.Contains("Showing <strong>@table.FirstRow", view, StringComparison.Ordinal);
        Assert.Contains("name=\"pageSize\"", view, StringComparison.Ordinal);
        Assert.Contains("section = sectionFilter, category = categoryFilter, location = locationFilter", view, StringComparison.Ordinal);
        Assert.Contains("vendor = vendorFilter, need = needFilter, status = statusFilter, q = searchFilter", view, StringComparison.Ordinal);
        Assert.Contains("new[] { 10, 25, 50, 100 }", view, StringComparison.Ordinal);
        Assert.Contains("lf-budget-page-links", css, StringComparison.Ordinal);
        Assert.Contains("NumericItemNumberKey", service, StringComparison.Ordinal);
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
