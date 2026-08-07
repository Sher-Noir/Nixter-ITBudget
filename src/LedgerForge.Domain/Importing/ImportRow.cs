using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Importing;

public enum ImportRowOutcome
{
    Pending,
    Accepted,
    AcceptedWithWarning,
    Rejected,
    Committed
}

public sealed class ImportRow : AuditableEntity
{
    private ImportRow() { }

    public ImportRow(Guid importBatchId, string sourceSheet, int sourceRowNumber, string? sourceKey, string rawDataJson)
    {
        if (importBatchId == Guid.Empty) throw new ArgumentException("Import batch is required.", nameof(importBatchId));
        if (string.IsNullOrWhiteSpace(sourceSheet)) throw new ArgumentException("Source sheet is required.", nameof(sourceSheet));
        if (sourceRowNumber < 1) throw new ArgumentOutOfRangeException(nameof(sourceRowNumber));
        if (string.IsNullOrWhiteSpace(rawDataJson)) throw new ArgumentException("Raw source-row snapshot is required.", nameof(rawDataJson));
        ImportBatchId = importBatchId;
        SourceSheet = sourceSheet.Trim();
        SourceRowNumber = sourceRowNumber;
        SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim();
        RawDataJson = rawDataJson;
        Outcome = ImportRowOutcome.Pending;
    }

    public Guid ImportBatchId { get; private set; }
    public string SourceSheet { get; private set; } = string.Empty;
    public int SourceRowNumber { get; private set; }
    public string? SourceKey { get; private set; }
    public string RawDataJson { get; private set; } = string.Empty;
    public ImportRowOutcome Outcome { get; private set; }
    public string? TargetEntityType { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public string? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? ReviewNote { get; private set; }

    public void Accept(bool withWarning = false)
    {
        EnsurePending();
        Outcome = withWarning ? ImportRowOutcome.AcceptedWithWarning : ImportRowOutcome.Accepted;
    }

    public void Reject()
    {
        EnsurePending();
        Outcome = ImportRowOutcome.Rejected;
    }

    public void OverrideReviewOutcome(
        ImportRowOutcome outcome,
        string actor,
        string note,
        DateTimeOffset reviewedAtUtc)
    {
        if (Outcome == ImportRowOutcome.Committed)
            throw new InvalidOperationException("Committed import rows cannot be reclassified.");
        if (outcome is not (ImportRowOutcome.Accepted or ImportRowOutcome.AcceptedWithWarning or ImportRowOutcome.Rejected))
            throw new ArgumentOutOfRangeException(nameof(outcome), "Review outcome must be Accepted, AcceptedWithWarning, or Rejected.");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Reviewer identity is required.", nameof(actor));
        if (actor.Trim().Length > 256) throw new ArgumentException("Reviewer identity cannot exceed 256 characters.", nameof(actor));
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Review note is required.", nameof(note));
        if (note.Trim().Length > 2000) throw new ArgumentException("Review note cannot exceed 2000 characters.", nameof(note));

        Outcome = outcome;
        ReviewedBy = actor.Trim();
        ReviewedAtUtc = reviewedAtUtc;
        ReviewNote = note.Trim();
    }

    public void MarkCommitted(string targetEntityType, Guid targetEntityId)
    {
        if (Outcome is not (ImportRowOutcome.Accepted or ImportRowOutcome.AcceptedWithWarning)) throw new InvalidOperationException($"Import row cannot be committed from outcome {Outcome}.");
        if (string.IsNullOrWhiteSpace(targetEntityType)) throw new ArgumentException("Target entity type is required.", nameof(targetEntityType));
        if (targetEntityId == Guid.Empty) throw new ArgumentException("Target entity ID is required.", nameof(targetEntityId));
        TargetEntityType = targetEntityType.Trim();
        TargetEntityId = targetEntityId;
        Outcome = ImportRowOutcome.Committed;
    }

    private void EnsurePending()
    {
        if (Outcome != ImportRowOutcome.Pending) throw new InvalidOperationException($"Import row cannot change outcome from {Outcome}.");
    }
}
