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
}
