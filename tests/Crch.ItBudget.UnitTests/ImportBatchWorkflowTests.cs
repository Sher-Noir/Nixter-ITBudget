using Crch.ItBudget.Domain.Importing;

namespace Crch.ItBudget.UnitTests;

public sealed class ImportBatchWorkflowTests
{
    [Fact]
    public void ReconciledPreview_CanBeAcceptedWithoutReasonAndCommitted()
    {
        var batch = new ImportBatch("FY2027_WORKBOOK", "budget.xlsx", new string('a', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(68, 68, 0, 830683.48m, 44, true, DateTimeOffset.UtcNow);

        batch.AcceptForCommit("DOMAIN\\budget.admin");
        batch.MarkCommitted(DateTimeOffset.UtcNow);

        Assert.Equal(ImportBatchStatus.Committed, batch.Status);
        Assert.Equal("DOMAIN\\budget.admin", batch.AcceptedBy);
        Assert.Null(batch.AcceptanceReason);
        Assert.NotNull(batch.CommittedAtUtc);
    }

    [Fact]
    public void UnreconciledPreview_RequiresAcceptanceReason()
    {
        var batch = new ImportBatch("FY2027_WORKBOOK", "budget.xlsx", new string('b', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(67, 67, 1, 800000m, 43, false, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => batch.AcceptForCommit("DOMAIN\\budget.admin"));

        batch.AcceptForCommit("DOMAIN\\budget.admin", "Reviewed source exception and approved migration variance.");
        Assert.Equal("Reviewed source exception and approved migration variance.", batch.AcceptanceReason);
    }

    [Fact]
    public void Commit_RequiresExplicitAcceptance()
    {
        var batch = new ImportBatch("FY2027_WORKBOOK", "budget.xlsx", new string('c', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(68, 68, 0, 830683.48m, 44, true, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => batch.MarkCommitted(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CommittedBatch_CannotBeRejected()
    {
        var batch = new ImportBatch("FY2027_WORKBOOK", "budget.xlsx", new string('d', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(68, 68, 0, 830683.48m, 44, true, DateTimeOffset.UtcNow);
        batch.AcceptForCommit("DOMAIN\\budget.admin");
        batch.MarkCommitted(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(batch.Reject);
    }
}
