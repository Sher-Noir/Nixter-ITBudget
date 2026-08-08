using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.MasterData;

public sealed class LookupValueAlias : AuditableEntity
{
    private LookupValueAlias() { }

    public LookupValueAlias(string lookupType, string sourceValue, string canonicalCode)
    {
        if (string.IsNullOrWhiteSpace(lookupType)) throw new ArgumentException("Lookup type is required.", nameof(lookupType));
        if (string.IsNullOrWhiteSpace(sourceValue)) throw new ArgumentException("Source value is required.", nameof(sourceValue));
        if (string.IsNullOrWhiteSpace(canonicalCode)) throw new ArgumentException("Canonical code is required.", nameof(canonicalCode));
        LookupType = lookupType.Trim();
        SourceValue = sourceValue.Trim();
        CanonicalCode = canonicalCode.Trim();
        IsActive = true;
    }

    public string LookupType { get; private set; } = string.Empty;
    public string SourceValue { get; private set; } = string.Empty;
    public string CanonicalCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}
