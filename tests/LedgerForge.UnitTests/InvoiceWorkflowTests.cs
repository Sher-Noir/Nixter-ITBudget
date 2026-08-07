using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Procurement;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class InvoiceWorkflowTests
{
    [Fact]
    public void Invoice_FollowsApprovalAndPostingSequence()
    {
        var invoice = NewInvoice();
        var now = DateTimeOffset.UtcNow;

        invoice.Submit("DOMAIN\\editor", now);
        Assert.Equal(InvoiceState.PendingApproval, invoice.State);

        invoice.Approve("DOMAIN\\approver", now.AddMinutes(1));
        Assert.Equal(InvoiceState.Approved, invoice.State);

        invoice.MarkPosted("DOMAIN\\poster", now.AddMinutes(2));
        Assert.Equal(InvoiceState.Posted, invoice.State);
        Assert.Equal("DOMAIN\\poster", invoice.PostedBy);
    }

    [Fact]
    public void Invoice_RejectionRequiresPendingApproval()
    {
        var invoice = NewInvoice();

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Reject("DOMAIN\\approver", "Not valid", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PostedInvoice_CannotBeCancelled()
    {
        var invoice = NewInvoice();
        var now = DateTimeOffset.UtcNow;
        invoice.Submit("DOMAIN\\editor", now);
        invoice.Approve("DOMAIN\\approver", now.AddMinutes(1));
        invoice.MarkPosted("DOMAIN\\poster", now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Cancel("DOMAIN\\editor", "Too late", now.AddMinutes(3)));
    }

    [Fact]
    public void InvoiceAllocation_CanPersistResolvedFiscalPeriod()
    {
        var allocation = new InvoiceAllocation(Guid.NewGuid(), 1, "Subscription", 250m);
        var periodId = Guid.NewGuid();

        allocation.AssignFiscalPeriod(periodId);

        Assert.Equal(periodId, allocation.FiscalPeriodId);
    }

    [Fact]
    public void InvoiceActual_PreservesInvoiceLineageThroughReversal()
    {
        var invoiceId = Guid.NewGuid();
        var actual = new ActualTransaction(
            Guid.NewGuid(),
            new DateOnly(2027, 2, 1),
            100m,
            "Invoice allocation",
            ActualTransactionKind.Invoice,
            "INV-1",
            invoiceId: invoiceId);

        var reversal = actual.CreateReversal(new DateOnly(2027, 2, 2), "Correction");

        Assert.Equal(invoiceId, actual.InvoiceId);
        Assert.Equal(invoiceId, reversal.InvoiceId);
    }

    private static Invoice NewInvoice()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "INV-1001",
            new DateOnly(2027, 2, 1),
            "Annual subscription",
            1000m);
}
