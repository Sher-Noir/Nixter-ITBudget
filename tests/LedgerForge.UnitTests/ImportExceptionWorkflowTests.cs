using LedgerForge.Domain.Importing;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ImportExceptionWorkflowTests
{
    [Fact]
    public void Resolve_RecordsActorNoteAndTimestamp()
    {
        var exception = new ImportException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UNKNOWN_DEPARTMENT",
            "Source department is not mapped.",
            ImportExceptionSeverity.Error);
        var resolvedAt = DateTimeOffset.UtcNow;

        exception.Assign("DOMAIN\\reviewer");
        exception.Resolve(
            ImportExceptionResolutionStatus.Accepted,
            "Reviewed and accepted for this migration.",
            "DOMAIN\\administrator",
            resolvedAt);

        Assert.Equal(ImportExceptionResolutionStatus.Accepted, exception.ResolutionStatus);
        Assert.Equal("DOMAIN\\reviewer", exception.AssignedTo);
        Assert.Equal("DOMAIN\\administrator", exception.ResolvedBy);
        Assert.Equal(resolvedAt, exception.ResolvedAtUtc);
        Assert.NotNull(exception.ResolutionNote);
        Assert.Throws<InvalidOperationException>(() => exception.Assign("DOMAIN\\other"));
    }

    [Fact]
    public void Resolve_RequiresNonOpenResolutionAndNote()
    {
        var exception = new ImportException(
            Guid.NewGuid(),
            null,
            "WORKBOOK_WARNING",
            "Review warning.",
            ImportExceptionSeverity.Warning);

        Assert.Throws<ArgumentException>(() => exception.Resolve(
            ImportExceptionResolutionStatus.Open,
            "Still open",
            "DOMAIN\\administrator",
            DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => exception.Resolve(
            ImportExceptionResolutionStatus.Accepted,
            "",
            "DOMAIN\\administrator",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Reopen_ClearsResolutionButKeepsAssignment()
    {
        var exception = new ImportException(
            Guid.NewGuid(),
            null,
            "SOURCE_TOTAL_MISMATCH",
            "Source total differs.",
            ImportExceptionSeverity.Warning);
        exception.Assign("DOMAIN\\reviewer");
        exception.Resolve(
            ImportExceptionResolutionStatus.Corrected,
            "Corrected through mapping review.",
            "DOMAIN\\administrator",
            DateTimeOffset.UtcNow);

        exception.Reopen();

        Assert.Equal(ImportExceptionResolutionStatus.Open, exception.ResolutionStatus);
        Assert.Null(exception.ResolutionNote);
        Assert.Null(exception.ResolvedBy);
        Assert.Null(exception.ResolvedAtUtc);
        Assert.Equal("DOMAIN\\reviewer", exception.AssignedTo);
    }
}
