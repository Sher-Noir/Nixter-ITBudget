using Xunit;

namespace LedgerForge.UiTests;

public sealed class PurchasedBudgetItemContractTests
{
    [Fact]
    public void PurchasedStatus_HasPermissionedActionsAndVisibleLabels()
    {
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Approvals", "ApprovalQueueService.cs");
        var detail = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var index = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");

        Assert.Contains("[HttpPost(\"items/{id:guid}/purchased\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"items/{id:guid}/undo-purchased\")]", controller, StringComparison.Ordinal);
        Assert.Contains("AuthorizationPolicies.EditPlanningBudget", controller, StringComparison.Ordinal);
        Assert.Contains("SetBudgetItemPurchasedAsync(", service, StringComparison.Ordinal);
        Assert.Contains("AuditAction.Explicit", service, StringComparison.Ordinal);
        Assert.Contains("BudgetItemStatus.Purchased", index, StringComparison.Ordinal);
        Assert.Contains("Mark Purchased", detail, StringComparison.Ordinal);
        Assert.Contains("Undo Purchased", detail, StringComparison.Ordinal);
        Assert.Contains("/budget/items/@selectedItem.Id/purchased", index, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchasedStatus_DoesNotChangeExistingApprovalOrActualTotals()
    {
        var domain = Read("src", "LedgerForge.Domain", "Budgeting", "BudgetItem.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Approvals", "ApprovalQueueService.cs");

        Assert.Contains("public void MarkPurchased()", domain, StringComparison.Ordinal);
        Assert.Contains("public void UndoPurchased(BudgetItemStatus? previousStatus = null)", domain, StringComparison.Ordinal);
        Assert.Contains("item.MarkPurchased();", service, StringComparison.Ordinal);
        Assert.Contains("item.UndoPurchased(restoreStatus);", service, StringComparison.Ordinal);
        Assert.Contains("ApprovalAndActualsUnchanged = true", service, StringComparison.Ordinal);
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
