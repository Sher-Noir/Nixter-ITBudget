using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Procurement;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class DraftRecordMaintenanceTests
{
    [Fact]
    public void DraftPurchaseOrder_CanBeEditedThenArchived_WithWorkflowAuditFields()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), "PO-100", "Original");
        order.UpdateDraft("PO-101", "Corrected");
        Assert.Equal("PO-101", order.Number);
        Assert.Equal("Corrected", order.Description);

        order.ArchiveDraft("test-user", "Duplicate", DateTimeOffset.UtcNow);
        Assert.True(order.IsArchived);
        Assert.Equal(PurchaseOrderState.Cancelled, order.State);
        Assert.Equal("Duplicate", order.CancellationReason);
        Assert.Throws<InvalidOperationException>(() => order.UpdateDraft("PO-102", "Another"));
    }

    [Fact]
    public void DraftPurchaseOrderLines_AreArchivedInsteadOfHardDeleted()
    {
        var line = new PurchaseOrderLine(Guid.NewGuid(), 1, "Hardware", 2m, 30m);
        line.Update("Hardware revised", 3m, 40m, null, null, null, null);
        Assert.Equal(120m, line.LineTotal);

        line.ArchiveDraftLine();
        Assert.True(line.IsArchived);
        Assert.Throws<InvalidOperationException>(() => line.ArchiveDraftLine());
    }

    [Fact]
    public void DraftInvoice_CanBeEditedThenArchived_WithoutPostingLedgerRows()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(), "INV-100",
            new DateOnly(2027, 1, 15), "Original", 100m);
        invoice.UpdateDraft("INV-101", new DateOnly(2027, 1, 16), "Corrected", 110m);
        Assert.Equal("INV-101", invoice.InvoiceNumber);
        Assert.Equal(110m, invoice.TotalAmount);

        invoice.ArchiveDraft("test-user", "Entered in error", DateTimeOffset.UtcNow);
        Assert.True(invoice.IsArchived);
        Assert.Equal(InvoiceState.Cancelled, invoice.State);
        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraft(
            "INV-102", new DateOnly(2027, 1, 17), "Another", 120m));
    }

    [Fact]
    public void DraftInvoiceAllocation_CanBeEditedAndArchived()
    {
        var allocation = new InvoiceAllocation(Guid.NewGuid(), 1, "Original", 90m);
        allocation.UpdateDraft("Corrected", 80m, null, null, null, null, null);
        Assert.Equal(80m, allocation.Amount);
        allocation.ArchiveDraftAllocation();
        Assert.True(allocation.IsArchived);
        Assert.Throws<InvalidOperationException>(() => allocation.UpdateDraft(
            "Another", 70m, null, null, null, null, null));
    }

    [Fact]
    public void ActualReversal_RetainsOriginalAndOffsetsItsAmount()
    {
        var original = new ActualTransaction(
            Guid.NewGuid(), new DateOnly(2027, 2, 1), 25m, "Original",
            ActualTransactionKind.Manual);
        var reversal = original.CreateReversal(new DateOnly(2027, 2, 2), "Correction");

        Assert.Equal(original.Id, reversal.ReversesTransactionId);
        Assert.Equal(-25m, reversal.Amount);
        Assert.Equal(ActualTransactionKind.Reversal, reversal.Kind);
        Assert.Equal("Correction", reversal.ReversalReason);
    }
}
