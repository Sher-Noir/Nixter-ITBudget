using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Security;

public enum UserRoleExceptionEffect
{
    Grant,
    Deny
}

public sealed class UserRoleException : AuditableEntity
{
    private UserRoleException() { }

    public UserRoleException(string domainIdentity, ApplicationRole role, UserRoleExceptionEffect effect, string reason)
    {
        if (string.IsNullOrWhiteSpace(domainIdentity)) throw new ArgumentException("Domain identity is required.", nameof(domainIdentity));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        DomainIdentity = domainIdentity.Trim();
        Role = role;
        Effect = effect;
        Reason = reason.Trim();
        IsActive = true;
    }

    public string DomainIdentity { get; private set; } = string.Empty;
    public ApplicationRole Role { get; private set; }
    public UserRoleExceptionEffect Effect { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public void Update(UserRoleExceptionEffect effect, string reason, DateTimeOffset? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        Effect = effect;
        Reason = reason.Trim();
        ExpiresAtUtc = expiresAtUtc;
        IsActive = true;
    }

    public void SetExpiration(DateTimeOffset? expiresAtUtc) => ExpiresAtUtc = expiresAtUtc;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
