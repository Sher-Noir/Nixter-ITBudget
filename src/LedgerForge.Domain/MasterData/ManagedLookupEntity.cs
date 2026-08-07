using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.MasterData;

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

    public void UpdateDisplay(string name, string? description, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Lookup name is required.", nameof(name));
        if (name.Trim().Length > 250) throw new ArgumentException("Lookup name cannot exceed 250 characters.", nameof(name));
        if (description is not null && description.Trim().Length > 1000) throw new ArgumentException("Lookup description cannot exceed 1000 characters.", nameof(description));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SortOrder = sortOrder;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
