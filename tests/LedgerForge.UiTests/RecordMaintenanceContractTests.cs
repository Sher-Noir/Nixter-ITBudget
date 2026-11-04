using Xunit;

namespace LedgerForge.UiTests;

public sealed class RecordMaintenanceContractTests
{
    [Fact]
    public void PurchaseOrderDraftMaintenance_IsSecuredAndWiredInDetails()
    {
        var controller = Read("src", "LedgerForge.Web", "Controllers", "PurchaseOrdersController.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "PurchaseOrders", "Details.cshtml");
        var service = Read("src", "LedgerForge.Infrastructure", "Procurement", "ProcurementService.cs");

        Assert.Contains("[HttpPost(\"{id:guid}/edit\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/delete\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/lines/{lineId:guid}/update\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/lines/{lineId:guid}/delete\")]", controller, StringComparison.Ordinal);
        Assert.Contains("/purchase-orders/@Model.Order.Id/edit", view, StringComparison.Ordinal);
        Assert.Contains("/purchase-orders/@Model.Order.Id/delete", view, StringComparison.Ordinal);
        Assert.Contains("ArchiveDraftAsync", service, StringComparison.Ordinal);
    }

    [Fact]
    public void InvoiceDraftMaintenance_ExcludesArchivedAllocationTotals()
    {
        var controller = Read("src", "LedgerForge.Web", "Controllers", "InvoicesController.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "Invoices", "Details.cshtml");
        var service = Read("src", "LedgerForge.Infrastructure", "Procurement", "InvoiceWorkflowService.cs");
        var query = Read("src", "LedgerForge.Infrastructure", "Procurement", "InvoiceQueryService.cs");

        Assert.Contains("[HttpPost(\"{id:guid}/edit\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/delete\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/allocations/{allocationId:guid}/edit\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/allocations/{allocationId:guid}/delete\")]", controller, StringComparison.Ordinal);
        Assert.Contains("/invoices/@Model.Invoice.Id/delete", view, StringComparison.Ordinal);
        Assert.Contains("ArchiveDraftAllocation", service, StringComparison.Ordinal);
        Assert.Contains("x.InvoiceId == id && !x.IsArchived", service, StringComparison.Ordinal);
        Assert.Contains("x.InvoiceId == id && !x.IsArchived", query, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualActualCorrection_IsAtomic_AndBudgetShowsActualRightOfTotal()
    {
        var ledger = Read("src", "LedgerForge.Infrastructure", "Actuals", "ActualLedgerService.cs");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "ActualsController.cs");
        var view = Read("src", "LedgerForge.Web", "Views", "Actuals", "Index.cshtml");
        var modal = Read("src", "LedgerForge.Web", "Views", "Actuals", "_Modal.cshtml");
        var budget = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var planning = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetPlanningService.cs");
        var table = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemTable.cs");

        Assert.Contains("BeginTransactionAsync(cancellationToken)", ledger, StringComparison.Ordinal);
        Assert.Contains("await ReverseAsync(original.Id, original.TransactionDate, reason, cancellationToken)", ledger, StringComparison.Ordinal);
        Assert.Contains("var replacementId = await PostManualAsync(", ledger, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"{id:guid}/correct\")]", controller, StringComparison.Ordinal);
        Assert.Contains("/actuals/@transaction.Id/correct", view, StringComparison.Ordinal);
        Assert.Contains("/actuals/@transaction.Id/correct", modal, StringComparison.Ordinal);
        Assert.Contains("Void with reversal", modal, StringComparison.Ordinal);
        Assert.Contains("Void with reversal", view, StringComparison.Ordinal);
        Assert.Contains("href=\"@SortUrl(\"actual\")\"", budget, StringComparison.Ordinal);
        Assert.Contains("x.Actual", table, StringComparison.Ordinal);
        Assert.Contains("group.Sum(x => x.Amount)", planning, StringComparison.Ordinal);
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
