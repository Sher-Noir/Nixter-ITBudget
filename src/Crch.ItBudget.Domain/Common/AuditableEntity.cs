namespace Crch.ItBudget.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public string CreatedBy { get; protected set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; protected set; }
    public string? ModifiedBy { get; protected set; }
    public DateTimeOffset? ModifiedAtUtc { get; protected set; }
    public bool IsArchived { get; protected set; }
    public byte[] RowVersion { get; protected set; } = [];
}
