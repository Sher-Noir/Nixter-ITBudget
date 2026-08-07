using Crch.ItBudget.Domain.Common;

namespace Crch.ItBudget.Domain.MasterData;

public abstract class ManagedLookupEntity : AuditableEntity
{
    protected ManagedLookupEntity() { }

    protected ManagedLookupEntity(string code, string name, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Lookup code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Lookup name is required.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));

        Code = code.Trim();
        Name = name.Trim();
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Code { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public string? Description { get; protected set; }
    public int SortOrder { get; protected set; }
    public bool IsActive { get; protected set; } = true;
}
