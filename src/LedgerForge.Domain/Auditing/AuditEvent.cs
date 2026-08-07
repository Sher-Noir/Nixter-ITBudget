namespace LedgerForge.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Archived,
    Explicit
}

public sealed class AuditEvent
{
    private AuditEvent() { }

    public AuditEvent(
        string actor,
        string entityType,
        Guid? entityId,
        AuditAction action,
        string? beforeJson,
        string? afterJson,
        string correlationId,
        DateTimeOffset occurredAtUtc,
        string? requestMethod = null,
        string? requestPath = null,
        string? remoteAddress = null,
        string? userAgent = null)
    {
        Id = Guid.NewGuid();
        Actor = Required(actor, 256, nameof(actor));
        EntityType = Required(entityType, 256, nameof(entityType));
        EntityId = entityId;
        Action = action;
        BeforeJson = Optional(beforeJson, 16000, nameof(beforeJson));
        AfterJson = Optional(afterJson, 16000, nameof(afterJson));
        CorrelationId = Required(correlationId, 128, nameof(correlationId));
        OccurredAtUtc = occurredAtUtc;
        RequestMethod = Optional(requestMethod, 32, nameof(requestMethod));
        RequestPath = Optional(requestPath, 2048, nameof(requestPath));
        RemoteAddress = Optional(remoteAddress, 128, nameof(remoteAddress));
        UserAgent = Optional(userAgent, 1000, nameof(userAgent));
    }

    public Guid Id { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? RequestMethod { get; private set; }
    public string? RequestPath { get; private set; }
    public string? RemoteAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private static string Required(string value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Required(value, max, parameterName);
    }
}
