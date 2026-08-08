using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Security;

public sealed class AdGroupMapping : AuditableEntity
{
    private AdGroupMapping() { }

    public AdGroupMapping(ApplicationRole role, string groupName, string? description = null)
    {
        Role = role;
        Update(groupName, description);
        IsActive = true;
    }

    public ApplicationRole Role { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string groupName, string? description)
    {
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("AD group name is required.", nameof(groupName));
        var normalizedGroup = groupName.Trim();
        if (normalizedGroup.Length > 256) throw new ArgumentException("AD group name cannot exceed 256 characters.", nameof(groupName));
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 1000) throw new ArgumentException("Description cannot exceed 1000 characters.", nameof(description));
        GroupName = normalizedGroup;
        Description = normalizedDescription;
    }

    public void UpdateGroupName(string groupName) => Update(groupName, Description);
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
