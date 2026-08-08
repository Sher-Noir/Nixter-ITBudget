using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Security;

public sealed class AdGroupMapping : AuditableEntity
{
    private AdGroupMapping() { }

    public AdGroupMapping(ApplicationRole role, string groupName, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("AD group name is required.", nameof(groupName));
        Role = role;
        GroupName = groupName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
    }

    public ApplicationRole Role { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdateGroupName(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("AD group name is required.", nameof(groupName));
        GroupName = groupName.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
