using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Procurement;

public sealed class PurchaseReceipt : AuditableEntity
{
    private PurchaseReceipt() { }

    public PurchaseReceipt(Guid purchaseOrderId, string receiptNumber, DateOnly receivedDate, string receivedBy, string? note = null)
    {
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("Purchase order is required.", nameof(purchaseOrderId));
        PurchaseOrderId = purchaseOrderId;
        ReceiptNumber = Required(receiptNumber, 100, nameof(receiptNumber));
        ReceivedDate = receivedDate;
        ReceivedBy = Required(receivedBy, 256, nameof(receivedBy));
        Note = Optional(note, 1000, nameof(note));
    }

    public Guid PurchaseOrderId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public DateOnly ReceivedDate { get; private set; }
    public string ReceivedBy { get; private set; } = string.Empty;
    public string? Note { get; private set; }

    private static string Required(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? null : Required(value, maxLength, parameterName);
}

public sealed class PurchaseReceiptLine : AuditableEntity
{
    private PurchaseReceiptLine() { }

    public PurchaseReceiptLine(Guid purchaseReceiptId, Guid purchaseOrderLineId, decimal quantityReceived, string? note = null)
    {
        if (purchaseReceiptId == Guid.Empty) throw new ArgumentException("Purchase receipt is required.", nameof(purchaseReceiptId));
        if (purchaseOrderLineId == Guid.Empty) throw new ArgumentException("Purchase order line is required.", nameof(purchaseOrderLineId));
        if (quantityReceived <= 0m) throw new ArgumentOutOfRangeException(nameof(quantityReceived));
        PurchaseReceiptId = purchaseReceiptId;
        PurchaseOrderLineId = purchaseOrderLineId;
        QuantityReceived = quantityReceived;
        Note = Optional(note, 500, nameof(note));
    }

    public Guid PurchaseReceiptId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public decimal QuantityReceived { get; private set; }
    public string? Note { get; private set; }

    private static string? Optional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}
