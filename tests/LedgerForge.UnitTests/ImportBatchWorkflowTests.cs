using LedgerForge.Domain.Importing;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ImportBatchWorkflowTests
{
    [Fact]
    public void ReconciledPreview_CanBeAcceptedWithoutReasonAndCommitted()
    {
        var batch = new ImportBatch("LEGACY_BUDGET_WORKBOOK", "budget.xlsx", new string('a', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(10, 10, 0, 125000m, 4, true, DateTimeOffset.UtcNow);

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
        var batch = new ImportBatch("LEGACY_BUDGET_WORKBOOK", "budget.xlsx", new string('b', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(9, 9, 1, 120000m, 3, false, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => batch.AcceptForCommit("DOMAIN\\budget.admin"));

        batch.AcceptForCommit("DOMAIN\\budget.admin", "Reviewed the source variance and accepted the import preview.");
        Assert.NotNull(batch.AcceptanceReason);
    }

    [Fact]
    public void Commit_RequiresExplicitAcceptance()
    {
        var batch = new ImportBatch("LEGACY_BUDGET_WORKBOOK", "budget.xlsx", new string('c', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(10, 10, 0, 125000m, 4, true, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => batch.MarkCommitted(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CommittedBatch_CannotBeRejected()
    {
        var batch = new ImportBatch("LEGACY_BUDGET_WORKBOOK", "budget.xlsx", new string('d', 64), 1000);
        batch.BeginValidation();
        batch.CompletePreview(10, 10, 0, 125000m, 4, true, DateTimeOffset.UtcNow);
        batch.AcceptForCommit("DOMAIN\\budget.admin");
        batch.MarkCommitted(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(batch.Reject);
    }
}
