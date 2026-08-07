using Crch.ItBudget.Domain.Common;

namespace Crch.ItBudget.Domain.Importing;

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

    public ImportRow(
        Guid importBatchId,
        string sourceSheet,
        int sourceRowNumber,
        string? sourceKey,
        string rawDataJson)
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

    public void MarkCommitted(string targetEntityType, Guid targetEntityId)
    {
        if (Outcome is not (ImportRowOutcome.Accepted or ImportRowOutcome.AcceptedWithWarning))
        {
            throw new InvalidOperationException($"Import row cannot be committed from outcome {Outcome}.");
        }
        if (string.IsNullOrWhiteSpace(targetEntityType)) throw new ArgumentException("Target entity type is required.", nameof(targetEntityType));
        if (targetEntityId == Guid.Empty) throw new ArgumentException("Target entity ID is required.", nameof(targetEntityId));

        TargetEntityType = targetEntityType.Trim();
        TargetEntityId = targetEntityId;
        Outcome = ImportRowOutcome.Committed;
    }

    private void EnsurePending()
    {
        if (Outcome != ImportRowOutcome.Pending)
        {
            throw new InvalidOperationException($"Import row cannot change outcome from {Outcome}.");
        }
    }
}
