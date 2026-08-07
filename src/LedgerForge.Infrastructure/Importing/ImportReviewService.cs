using LedgerForge.Domain.Importing;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Importing;

public sealed record ImportBatchSummary(
    Guid Id,
    string ImportType,
    string SourceFileName,
    string SourceSha256,
    ImportBatchStatus Status,
    DateTimeOffset CreatedAtUtc,
    int? SourceRowCount,
    int? AcceptedRowCount,
    int? ExceptionCount,
    decimal? RecalculatedPlannedTotal,
    bool? ReconciledToExpectedTargets,
    string? AcceptedBy);

public sealed record ImportRowSummary(
    Guid Id,
    string SourceSheet,
    int SourceRowNumber,
    string? SourceKey,
    ImportRowOutcome Outcome,
    string RawDataJson,
    string? TargetEntityType,
    Guid? TargetEntityId);

public sealed record ImportExceptionSummary(
    Guid Id,
    Guid? ImportRowId,
    string Code,
    string Message,
    ImportExceptionSeverity Severity,
    ImportExceptionResolutionStatus ResolutionStatus,
    string? AssignedTo,
    string? ResolutionNote,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAtUtc);

public sealed record ImportBatchDetail(
    ImportBatchSummary Batch,
    IReadOnlyList<ImportRowSummary> Rows,
    IReadOnlyList<ImportExceptionSummary> Exceptions,
    int OpenErrorCount,
    int OpenWarningCount,
    int RejectedRowCount,
    bool CanAcceptPreview);

public sealed class ImportReviewService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<ImportBatchSummary>> ListBatchesAsync(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);
        return await dbContext.ImportBatches
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new ImportBatchSummary(
                x.Id,
                x.ImportType,
                x.SourceFileName,
                x.SourceSha256,
                x.Status,
                x.CreatedAtUtc,
                x.SourceRowCount,
                x.AcceptedRowCount,
                x.ExceptionCount,
                x.RecalculatedPlannedTotal,
                x.ReconciledToExpectedTargets,
                x.AcceptedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<ImportBatchDetail?> GetBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Import batch ID is required.", nameof(batchId));

        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Where(x => x.Id == batchId)
            .Select(x => new ImportBatchSummary(
                x.Id,
                x.ImportType,
                x.SourceFileName,
                x.SourceSha256,
                x.Status,
                x.CreatedAtUtc,
                x.SourceRowCount,
                x.AcceptedRowCount,
                x.ExceptionCount,
                x.RecalculatedPlannedTotal,
                x.ReconciledToExpectedTargets,
                x.AcceptedBy))
            .SingleOrDefaultAsync(cancellationToken);
        if (batch is null) return null;

        var rows = await dbContext.ImportRows
            .AsNoTracking()
            .Where(x => x.ImportBatchId == batchId)
            .OrderBy(x => x.SourceSheet)
            .ThenBy(x => x.SourceRowNumber)
            .Select(x => new ImportRowSummary(
                x.Id,
                x.SourceSheet,
                x.SourceRowNumber,
                x.SourceKey,
                x.Outcome,
                x.RawDataJson,
                x.TargetEntityType,
                x.TargetEntityId))
            .ToListAsync(cancellationToken);

        var exceptions = await dbContext.ImportExceptions
            .AsNoTracking()
            .Where(x => x.ImportBatchId == batchId)
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.ResolutionStatus)
            .ThenBy(x => x.Code)
            .Select(x => new ImportExceptionSummary(
                x.Id,
                x.ImportRowId,
                x.Code,
                x.Message,
                x.Severity,
                x.ResolutionStatus,
                x.AssignedTo,
                x.ResolutionNote,
                x.ResolvedBy,
                x.ResolvedAtUtc))
            .ToListAsync(cancellationToken);

        var openErrorCount = exceptions.Count(x => x.Severity == ImportExceptionSeverity.Error && x.ResolutionStatus == ImportExceptionResolutionStatus.Open);
        var openWarningCount = exceptions.Count(x => x.Severity == ImportExceptionSeverity.Warning && x.ResolutionStatus == ImportExceptionResolutionStatus.Open);
        var rejectedRowCount = rows.Count(x => x.Outcome == ImportRowOutcome.Rejected);
        var canAcceptPreview = batch.Status == ImportBatchStatus.PreviewReady && batch.AcceptedBy is null && openErrorCount == 0;

        return new(batch, rows, exceptions, openErrorCount, openWarningCount, rejectedRowCount, canAcceptPreview);
    }

    public async Task ResolveExceptionAsync(
        Guid exceptionId,
        ImportExceptionResolutionStatus resolutionStatus,
        string resolutionNote,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var exception = await dbContext.ImportExceptions
            .SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Import exception was not found.");

        await EnsureBatchReviewableAsync(exception.ImportBatchId, cancellationToken);
        exception.Resolve(resolutionStatus, resolutionNote, actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenExceptionAsync(
        Guid exceptionId,
        CancellationToken cancellationToken = default)
    {
        var exception = await dbContext.ImportExceptions
            .SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Import exception was not found.");

        await EnsureBatchReviewableAsync(exception.ImportBatchId, cancellationToken);
        exception.Reopen();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignExceptionAsync(
        Guid exceptionId,
        string? assignedTo,
        CancellationToken cancellationToken = default)
    {
        var exception = await dbContext.ImportExceptions
            .SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("Import exception was not found.");

        await EnsureBatchReviewableAsync(exception.ImportBatchId, cancellationToken);
        exception.Assign(assignedTo);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptPreviewAsync(
        Guid batchId,
        string actor,
        string? acceptanceReason,
        CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Import batch was not found.");

        if (!string.IsNullOrWhiteSpace(batch.AcceptedBy))
            throw new InvalidOperationException("Import preview has already been accepted.");

        var openErrors = await dbContext.ImportExceptions.CountAsync(
            x => x.ImportBatchId == batchId &&
                 x.Severity == ImportExceptionSeverity.Error &&
                 x.ResolutionStatus == ImportExceptionResolutionStatus.Open,
            cancellationToken);
        if (openErrors > 0)
            throw new InvalidOperationException("Resolve all open error exceptions before accepting the import preview.");

        var rejectedRows = await dbContext.ImportRows.CountAsync(
            x => x.ImportBatchId == batchId && x.Outcome == ImportRowOutcome.Rejected,
            cancellationToken);
        if (rejectedRows > 0 && string.IsNullOrWhiteSpace(acceptanceReason))
            throw new InvalidOperationException("A written acceptance reason is required when the preview contains rejected source rows.");

        batch.AcceptForCommit(actor, acceptanceReason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await dbContext.ImportBatches
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Import batch was not found.");
        batch.Reject();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBatchReviewableAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .SingleAsync(x => x.Id == batchId, cancellationToken);

        if (batch.Status is ImportBatchStatus.Committed or ImportBatchStatus.Rejected || !string.IsNullOrWhiteSpace(batch.AcceptedBy))
            throw new InvalidOperationException("This import batch is no longer open for exception review.");
    }
}
