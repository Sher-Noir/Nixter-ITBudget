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
