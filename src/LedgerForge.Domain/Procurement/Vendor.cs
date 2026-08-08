using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Procurement;

public sealed class Vendor : AuditableEntity
{
    private Vendor() { }

    public Vendor(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Vendor code is required.", nameof(code));
        if (code.Trim().Length > 100) throw new ArgumentException("Vendor code cannot exceed 100 characters.", nameof(code));
        Code = code.Trim();
        Update(name, null, null, null);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, string? contactName, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Vendor name is required.", nameof(name));
        if (name.Trim().Length > 250) throw new ArgumentException("Vendor name cannot exceed 250 characters.", nameof(name));
        Name = name.Trim();
        ContactName = NormalizeOptional(contactName, 250, nameof(contactName));
        Email = NormalizeOptional(email, 320, nameof(email));
        Phone = NormalizeOptional(phone, 100, nameof(phone));
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}
