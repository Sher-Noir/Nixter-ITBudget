using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetItemActualCorrectionContractTests
{
    [Fact]
    public void BudgetItemActuals_HavePermissionedPrefilledCorrectionDialogAndAuditHistory()
    {
        var detail = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var workspace = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemWorkspaceService.cs");

        Assert.Contains("workspace.ActualCorrectionsAllowed", detail, StringComparison.Ordinal);
        Assert.Contains("actualRow.Kind == ActualTransactionKind.Manual && !actualRow.IsReversed", detail, StringComparison.Ordinal);
        Assert.Contains("data-edit-actual", detail, StringComparison.Ordinal);
        Assert.Contains("lf-actual-edit-dialog", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"transactionDate\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"sourceReference\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"description\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"financeAccountId\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"departmentId\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"locationId\"", detail, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", detail, StringComparison.Ordinal);
        Assert.Contains("Save Changes", detail, StringComparison.Ordinal);
        Assert.Contains("data-cancel-actual", detail, StringComparison.Ordinal);
        Assert.Contains("actualRow.ReversalReason", detail, StringComparison.Ordinal);
        Assert.Contains("actualRow.CreatedBy", detail, StringComparison.Ordinal);
        Assert.Contains("ActualTransactionKind.Reversal", detail, StringComparison.Ordinal);
        Assert.Contains("IsReversed", workspace, StringComparison.Ordinal);
        Assert.Contains("FiscalYearStatus.Closed", workspace, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.PostActuals", controller, StringComparison.Ordinal);
        Assert.Contains("CorrectBudgetItemManualAsync", controller, StringComparison.Ordinal);
        Assert.Contains("ActualCorrected", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderActions_UseSingleResponsiveFlexChildWithoutChangingEndpoints()
    {
        var detail = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-item.css");
        var js = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-budget.js");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");

        Assert.Contains("lf-item-hero-actions", detail, StringComparison.Ordinal);
        Assert.Contains("display: flex;", css, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-end", css, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1250px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", css, StringComparison.Ordinal);
        Assert.Contains("Mark Purchased", detail, StringComparison.Ordinal);
        Assert.Contains("Undo Purchased", detail, StringComparison.Ordinal);
        Assert.Contains("Record Actual", detail, StringComparison.Ordinal);
        Assert.Contains("/budget/items/@item.Id/purchased", detail, StringComparison.Ordinal);
        Assert.Contains("/budget/items/@item.Id/undo-purchased", detail, StringComparison.Ordinal);
        Assert.Contains("data-open-actual-entry", detail, StringComparison.Ordinal);
        Assert.Contains("data-edit-actual", js, StringComparison.Ordinal);
        Assert.Contains("data.submitted", js.Replace("dataset", "data"), StringComparison.Ordinal);
        Assert.Contains("SetBudgetItemPurchasedAsync", controller, StringComparison.Ordinal);
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
