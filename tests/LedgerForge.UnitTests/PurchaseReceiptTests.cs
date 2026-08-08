using LedgerForge.Domain.Procurement;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class PurchaseReceiptTests
{
    [Fact]
    public void Receipt_NormalizesReferenceAndActor()
    {
        var receipt = new PurchaseReceipt(Guid.NewGuid(), "  DEL-42  ", new DateOnly(2027, 2, 1), "  DOMAIN\\receiver  ", "  Dock A  ");

        Assert.Equal("DEL-42", receipt.ReceiptNumber);
        Assert.Equal("DOMAIN\\receiver", receipt.ReceivedBy);
        Assert.Equal("Dock A", receipt.Note);
    }

    [Fact]
    public void ReceiptLine_RequiresPositiveQuantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseReceiptLine(Guid.NewGuid(), Guid.NewGuid(), 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PurchaseReceiptLine(Guid.NewGuid(), Guid.NewGuid(), -1m));
    }

    [Fact]
    public void ReceiptLine_PreservesFractionalQuantity()
    {
        var line = new PurchaseReceiptLine(Guid.NewGuid(), Guid.NewGuid(), 1.1250m, "Partial delivery");

        Assert.Equal(1.1250m, line.QuantityReceived);
        Assert.Equal("Partial delivery", line.Note);
    }
}
