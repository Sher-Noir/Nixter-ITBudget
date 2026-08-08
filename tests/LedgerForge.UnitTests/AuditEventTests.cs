using LedgerForge.Domain.Auditing;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class AuditEventTests
{
    [Fact]
    public void AuditEvent_RequiresActorEntityAndCorrelation()
    {
        Assert.Throws<ArgumentException>(() => new AuditEvent(
            "",
            "BudgetItem",
            Guid.NewGuid(),
            AuditAction.Updated,
            null,
            "{}",
            "corr-1",
            DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => new AuditEvent(
            "DOMAIN\\user",
            "",
            Guid.NewGuid(),
            AuditAction.Updated,
            null,
            "{}",
            "corr-1",
            DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => new AuditEvent(
            "DOMAIN\\user",
            "BudgetItem",
            Guid.NewGuid(),
            AuditAction.Updated,
            null,
            "{}",
            "",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AuditEvent_PreservesRequestAndSnapshotMetadata()
    {
        var entityId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var audit = new AuditEvent(
            "DOMAIN\\approver",
            "Invoice",
            entityId,
            AuditAction.Updated,
            "{\"State\":\"PendingApproval\"}",
            "{\"State\":\"Approved\"}",
            "trace-123",
            occurredAt,
            "POST",
            "/invoices/1/approve",
            "127.0.0.1",
            "test-agent");

        Assert.Equal("DOMAIN\\approver", audit.Actor);
        Assert.Equal("Invoice", audit.EntityType);
        Assert.Equal(entityId, audit.EntityId);
        Assert.Equal(AuditAction.Updated, audit.Action);
        Assert.Equal("trace-123", audit.CorrelationId);
        Assert.Equal(occurredAt, audit.OccurredAtUtc);
        Assert.NotNull(audit.BeforeJson);
        Assert.NotNull(audit.AfterJson);
    }
}
