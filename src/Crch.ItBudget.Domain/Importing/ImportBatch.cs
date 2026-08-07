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

    public void BeginValidation()
    {
        if (Status != ImportBatchStatus.Uploaded)
        {
            throw new InvalidOperationException($"Import batch cannot begin validation from status {Status}.");
        }

        Status = ImportBatchStatus.Validating;
    }

    public void CompletePreview(
        int sourceRowCount,
        int acceptedRowCount,
        int exceptionCount,
        decimal recalculatedPlannedTotal,
        int mustHaveCount,
        bool reconciledToExpectedTargets,
        DateTimeOffset generatedAtUtc)
    {
        if (Status != ImportBatchStatus.Validating)
        {
            throw new InvalidOperationException($"Import preview cannot complete from status {Status}.");
        }
        if (sourceRowCount < 0) throw new ArgumentOutOfRangeException(nameof(sourceRowCount));
        if (acceptedRowCount < 0 || acceptedRowCount > sourceRowCount) throw new ArgumentOutOfRangeException(nameof(acceptedRowCount));
        if (exceptionCount < 0) throw new ArgumentOutOfRangeException(nameof(exceptionCount));
        if (recalculatedPlannedTotal < 0m) throw new ArgumentOutOfRangeException(nameof(recalculatedPlannedTotal));
        if (mustHaveCount < 0 || mustHaveCount > sourceRowCount) throw new ArgumentOutOfRangeException(nameof(mustHaveCount));

        SourceRowCount = sourceRowCount;
        AcceptedRowCount = acceptedRowCount;
        ExceptionCount = exceptionCount;
        RecalculatedPlannedTotal = recalculatedPlannedTotal;
        MustHaveCount = mustHaveCount;
        ReconciledToExpectedTargets = reconciledToExpectedTargets;
        PreviewGeneratedAtUtc = generatedAtUtc;
        Status = ImportBatchStatus.PreviewReady;
    }

    public void MarkValidationFailed()
    {
        if (Status is not (ImportBatchStatus.Uploaded or ImportBatchStatus.Validating))
        {
            throw new InvalidOperationException($"Import batch cannot fail validation from status {Status}.");
        }

        Status = ImportBatchStatus.ValidationFailed;
    }

    public void AcceptForCommit(string actor, string? acceptanceReason = null)
    {
        if (Status != ImportBatchStatus.PreviewReady)
        {
            throw new InvalidOperationException($"Import batch cannot be accepted from status {Status}.");
        }
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        if (ReconciledToExpectedTargets != true && string.IsNullOrWhiteSpace(acceptanceReason))
        {
            throw new InvalidOperationException("A reason is required when reconciliation targets are not met.");
        }

        AcceptedBy = actor.Trim();
        AcceptanceReason = string.IsNullOrWhiteSpace(acceptanceReason) ? null : acceptanceReason.Trim();
    }

    public void MarkCommitted(DateTimeOffset committedAtUtc)
    {
        if (Status != ImportBatchStatus.PreviewReady)
        {
            throw new InvalidOperationException($"Import batch cannot be committed from status {Status}.");
        }
        if (string.IsNullOrWhiteSpace(AcceptedBy))
        {
            throw new InvalidOperationException("Import batch must be explicitly accepted before commit.");
        }

        CommittedAtUtc = committedAtUtc;
        Status = ImportBatchStatus.Committed;
    }

    public void Reject()
    {
        if (Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Committed import batches cannot be rejected.");
        }

        Status = ImportBatchStatus.Rejected;
    }
}
