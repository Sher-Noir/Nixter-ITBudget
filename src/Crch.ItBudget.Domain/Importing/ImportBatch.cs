using Crch.ItBudget.Domain.Common;

namespace Crch.ItBudget.Domain.Importing;

public enum ImportBatchStatus
{
    Uploaded,
    Validating,
    PreviewReady,
    ValidationFailed,
    Committed,
    Rejected
}

public sealed class ImportBatch : AuditableEntity
{
    private ImportBatch() { }

    public ImportBatch(string importType, string sourceFileName, string sourceSha256, long sourceSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(importType)) throw new ArgumentException("Import type is required.", nameof(importType));
        if (string.IsNullOrWhiteSpace(sourceFileName)) throw new ArgumentException("Source file name is required.", nameof(sourceFileName));
        if (string.IsNullOrWhiteSpace(sourceSha256)) throw new ArgumentException("Source SHA-256 is required.", nameof(sourceSha256));
        if (sourceSizeBytes < 0) throw new ArgumentOutOfRangeException(nameof(sourceSizeBytes));

        ImportType = importType.Trim();
        SourceFileName = sourceFileName.Trim();
        SourceSha256 = sourceSha256.Trim().ToLowerInvariant();
        SourceSizeBytes = sourceSizeBytes;
        Status = ImportBatchStatus.Uploaded;
    }

    public string ImportType { get; private set; } = string.Empty;
    public string SourceFileName { get; private set; } = string.Empty;
    public string SourceSha256 { get; private set; } = string.Empty;
    public long SourceSizeBytes { get; private set; }
    public ImportBatchStatus Status { get; private set; }
    public int? SourceRowCount { get; private set; }
    public int? AcceptedRowCount { get; private set; }
    public int? ExceptionCount { get; private set; }
    public decimal? RecalculatedPlannedTotal { get; private set; }
    public int? MustHaveCount { get; private set; }
    public bool? ReconciledToExpectedTargets { get; private set; }
    public DateTimeOffset? PreviewGeneratedAtUtc { get; private set; }
    public DateTimeOffset? CommittedAtUtc { get; private set; }
    public string? AcceptedBy { get; private set; }
    public string? AcceptanceReason { get; private set; }
}
