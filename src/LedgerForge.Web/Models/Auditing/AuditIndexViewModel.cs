using LedgerForge.Domain.Auditing;

namespace LedgerForge.Web.Models.Auditing;

public sealed record AuditEventViewModel(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string EntityType,
    Guid? EntityId,
    AuditAction Action,
    string CorrelationId,
    string? RequestMethod,
    string? RequestPath,
    string? RemoteAddress,
    string? UserAgent,
    string? BeforeJson,
    string? AfterJson);

public sealed record AuditIndexViewModel(
    IReadOnlyList<AuditEventViewModel> Events,
    string? Actor,
    string? EntityType,
    Guid? EntityId,
    string? CorrelationId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Take);
