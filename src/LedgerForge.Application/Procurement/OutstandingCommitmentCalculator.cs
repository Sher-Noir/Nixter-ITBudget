namespace LedgerForge.Application.Procurement;

public static class OutstandingCommitmentCalculator
{
    public static decimal Calculate(decimal issuedPurchaseOrderTotal, decimal postedLinkedInvoiceTotal)
    {
        if (issuedPurchaseOrderTotal < 0m)
            throw new ArgumentOutOfRangeException(nameof(issuedPurchaseOrderTotal));
        if (postedLinkedInvoiceTotal < 0m)
            throw new ArgumentOutOfRangeException(nameof(postedLinkedInvoiceTotal));

        return Math.Max(0m, issuedPurchaseOrderTotal - postedLinkedInvoiceTotal);
    }
}
