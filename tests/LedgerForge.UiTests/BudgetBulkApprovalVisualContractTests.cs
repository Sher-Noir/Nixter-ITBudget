using Xunit;

namespace LedgerForge.UiTests;

public sealed class BudgetBulkApprovalVisualContractTests
{
    [Fact]
    public void BulkApproval_FormHasDedicatedLayoutAndNormalSizedCheckbox()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var css = Read("src", "LedgerForge.Web", "wwwroot", "css", "ledgerforge-budget.css");
        Assert.Contains("class=\"lf-bulk-approval-form\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"lf-bulk-approval-intro\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"lf-bulk-approval-confirm\"", view, StringComparison.Ordinal);
        Assert.Contains("class=\"lf-bulk-approval-actions\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/budget/bulk-submit\" class=\"lf-toolbar\"", view, StringComparison.Ordinal);
        Assert.Contains("#budget-bulk-submit.lf-bulk-approval-form", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas:", css, StringComparison.Ordinal);
        Assert.Contains("input[type=\"checkbox\"]", css, StringComparison.Ordinal);
        Assert.Contains("width: 17px;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 17px;", css, StringComparison.Ordinal);
        Assert.Contains("height: 40px;", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1100px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 480px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void OverUnderDisclosure_ExplainsExpectedSpendAndItsSources()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var query = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemSpendProjectionQuery.cs");
        var helper = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetSpendPacingCalculator.cs");
        Assert.Contains("id=\"budget-over-under-explanation\"", view, StringComparison.Ordinal);
        Assert.Contains("How is Current Over/Under calculated?", view, StringComparison.Ordinal);
        Assert.Contains("Expected through today", view, StringComparison.Ordinal);
        Assert.Contains("expectedUndatedPacedToDate", view, StringComparison.Ordinal);
        Assert.Contains("ExpectedOneTimeToDate(", query, StringComparison.Ordinal);
        Assert.Contains("FiscalYears.AsNoTracking()", query, StringComparison.Ordinal);
        Assert.Contains("actual > 0m", query, StringComparison.Ordinal);
        Assert.Contains("return 0m;", helper, StringComparison.Ordinal);
        Assert.Contains("ExpectedInstallmentsToDate", query, StringComparison.Ordinal);
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
        throw new DirectoryNotFoundException("LedgerForge root not found.");
    }
}
