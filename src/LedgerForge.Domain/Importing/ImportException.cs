using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Importing;

public enum ImportExceptionSeverity { Warning, Error }
public enum ImportExceptionResolutionStatus { Open, Accepted, Corrected, Rejected }

public sealed class ImportException : AuditableEntity
{
    private ImportException() { }

    public ImportException(Guid importBatchId, Guid? importRowId, string code, string message, ImportExceptionSeverity severity)
    {
        if (importBatchId == Guid.Empty) throw new ArgumentException("Import batch is required.", nameof(importBatchId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Exception code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Exception message is required.", nameof(message));
        ImportBatchId = importBatchId;
        ImportRowId = importRowId;
        Code = code.Trim();
        Message = message.Trim();
        Severity = severity;
        ResolutionStatus = ImportExceptionResolutionStatus.Open;
    }

    public Guid ImportBatchId { get; private set; }
    public Guid? ImportRowId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public ImportExceptionSeverity Severity { get; private set; }
    public ImportExceptionResolutionStatus ResolutionStatus { get; private set; }
    public string? AssignedTo { get; private set; }
    public string? ResolutionNote { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public void Assign(string? assignedTo)
    {
        if (ResolutionStatus != ImportExceptionResolutionStatus.Open)
            throw new InvalidOperationException("Resolved import exceptions cannot be reassigned.");
        AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim();
    }

    public void Resolve(
        ImportExceptionResolutionStatus resolutionStatus,
        string resolutionNote,
        string actor,
        DateTimeOffset resolvedAtUtc)
    {
        if (resolutionStatus == ImportExceptionResolutionStatus.Open)
            throw new ArgumentException("A resolved exception must use Accepted, Corrected, or Rejected status.", nameof(resolutionStatus));
        if (ResolutionStatus != ImportExceptionResolutionStatus.Open)
            throw new InvalidOperationException("Import exception is already resolved.");
        if (string.IsNullOrWhiteSpace(resolutionNote)) throw new ArgumentException("Resolution note is required.", nameof(resolutionNote));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Resolving actor is required.", nameof(actor));

        ResolutionStatus = resolutionStatus;
        ResolutionNote = resolutionNote.Trim();
        ResolvedBy = actor.Trim();
        ResolvedAtUtc = resolvedAtUtc;
    }

    public void Reopen()
    {
        ResolutionStatus = ImportExceptionResolutionStatus.Open;
        ResolutionNote = null;
        ResolvedBy = null;
        ResolvedAtUtc = null;
    }
}
