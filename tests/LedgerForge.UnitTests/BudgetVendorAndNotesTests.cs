using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class BudgetVendorAndNotesTests
{
    [Fact]
    public void DraftItem_AllowsVendorAndNotes_WithoutChangingBudgetTotal()
    {
        var item = NewItem();
        var vendor = Guid.NewGuid();

        item.UpdateVendorAndNotes(vendor, "  Requires additional review.  ");

        Assert.Equal(vendor, item.VendorId);
        Assert.Equal("Requires additional review.", item.Notes);
        Assert.Equal(250m, item.PlannedTotal);
    }

    [Fact]
    public void DraftItem_CanClearVendorAndNotes()
    {
        var item = NewItem();
        item.UpdateVendorAndNotes(Guid.NewGuid(), "Temporary");
        item.UpdateVendorAndNotes(null, " ");

        Assert.Null(item.VendorId);
        Assert.Null(item.Notes);
    }

    [Fact]
    public void VendorAndNotes_RejectInvalidIdentifiersAndOversizeNotes()
    {
        var item = NewItem();
        Assert.Throws<ArgumentException>(() => item.UpdateVendorAndNotes(Guid.Empty, null));
        Assert.Throws<ArgumentException>(() => item.UpdateVendorAndNotes(null, new string('x', 2001)));
    }

    [Fact]
    public void SubmittedItem_CannotChangeVendorOrNotes()
    {
        var item = NewItem();
        item.Submit("TEST\\budget-editor", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => item.UpdateVendorAndNotes(Guid.NewGuid(), "Updated"));
        Assert.Null(item.VendorId);
        Assert.Null(item.Notes);
    }

    private static BudgetItem NewItem()
        => new(Guid.NewGuid(), Guid.NewGuid(), "LF-TEST-01", "100", "Sample equipment", 5m, 50m);
}
