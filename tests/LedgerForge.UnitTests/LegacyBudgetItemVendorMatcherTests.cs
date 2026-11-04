using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class LegacyBudgetItemVendorMatcherTests
{
    private static readonly LegacyBudgetItemVendorMatcher.VendorIdentity Mvix =
        new(Guid.Parse("1f1bc5af-bc4c-4c51-9715-a61b9eaf68a9"), "MVIX", "Mvix");

    private static readonly LegacyBudgetItemVendorMatcher.VendorIdentity Meraki =
        new(Guid.Parse("7a0b4605-a964-45ec-b8d3-87ff476d2584"), "MERAKI", "Meraki");

    [Theory]
    [InlineData("MVIX - Digital Signage")]
    [InlineData("mvix - Digital Signage")]
    [InlineData("Mvix - Digital Signage")]
    public void MatchDescriptionPrefix_UsesExactVendorCodeOrName(string description)
    {
        Assert.Equal(Mvix.Id, LegacyBudgetItemVendorMatcher.MatchDescriptionPrefix(description, [Mvix, Meraki]));
    }

    [Fact]
    public void MatchDescriptionPrefix_MatchesVendorNameEvenIfCodeDiffers()
    {
        var vendor = new LegacyBudgetItemVendorMatcher.VendorIdentity(Guid.NewGuid(), "CISCO-MERAKI", "Meraki");
        Assert.Equal(vendor.Id, LegacyBudgetItemVendorMatcher.MatchDescriptionPrefix(
            "Meraki - Network Equipment", [vendor]));
    }

    [Theory]
    [InlineData("Digital Signage MVIX")]
    [InlineData("Digital Signage - MVIX")]
    [InlineData("MVIX digital signage")]
    [InlineData(null)]
    public void MatchDescriptionPrefix_NeverGuessesFromSubstrings(string? description)
    {
        Assert.Null(LegacyBudgetItemVendorMatcher.MatchDescriptionPrefix(description, [Mvix, Meraki]));
    }

    [Fact]
    public void MatchDescriptionPrefix_ReturnsNullOnAmbiguousVendor()
    {
        var duplicateCode = new LegacyBudgetItemVendorMatcher.VendorIdentity(Guid.NewGuid(), "MVIX", "Other Company");
        Assert.Null(LegacyBudgetItemVendorMatcher.MatchDescriptionPrefix(
            "MVIX - Digital Signage", [Mvix, duplicateCode]));
    }

    [Fact]
    public void MatchDescriptionPrefix_DoesNotTreatRepeatedIdAsAmbiguous()
    {
        Assert.Equal(Mvix.Id, LegacyBudgetItemVendorMatcher.MatchDescriptionPrefix(
            "MVIX - Digital Signage", [Mvix, Mvix]));
    }
}
