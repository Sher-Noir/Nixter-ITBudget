using LedgerForge.Domain.Importing;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ImportRowReviewTests
{
    [Fact]
    public void ReviewDisposition_CanMoveRejectedRowToAcceptedWithLineage()
    {
        var row = new ImportRow(
            Guid.NewGuid(),
            "Budget Lines",
            12,
            "12",
            "{\"itemNumber\":12}");
        row.Reject();
        var reviewedAt = DateTimeOffset.UtcNow;

        row.ReviewDisposition(
            ImportRowOutcome.Accepted,
            "DOMAIN\\reviewer",
            "Lookup mapping was corrected and the row was revalidated.",
            reviewedAt);

        Assert.Equal(ImportRowOutcome.Accepted, row.Outcome);
        Assert.Equal("DOMAIN\\reviewer", row.ReviewedBy);
        Assert.Equal(reviewedAt, row.ReviewedAtUtc);
        Assert.Contains("revalidated", row.ReviewNote);
    }

    [Fact]
    public void ReviewDisposition_RequiresReviewerAndNote()
    {
        var row = new ImportRow(
            Guid.NewGuid(),
            "Budget Lines",
            4,
            "4",
            "{\"itemNumber\":4}");
        row.Reject();

        Assert.Throws<ArgumentException>(() => row.ReviewDisposition(
            ImportRowOutcome.Accepted,
            "",
            "Reviewed",
            DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => row.ReviewDisposition(
            ImportRowOutcome.Accepted,
            "DOMAIN\\reviewer",
            "",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CommittedRow_CannotBeReviewedAgain()
    {
        var row = new ImportRow(
            Guid.NewGuid(),
            "Budget Lines",
            2,
            "2",
            "{\"itemNumber\":2}");
        row.Accept();
        row.MarkCommitted("BudgetItem", Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => row.ReviewDisposition(
            ImportRowOutcome.Rejected,
            "DOMAIN\\reviewer",
            "Attempted reversal.",
            DateTimeOffset.UtcNow));
    }
}
