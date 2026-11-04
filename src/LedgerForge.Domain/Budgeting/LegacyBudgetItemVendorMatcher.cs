namespace LedgerForge.Domain.Budgeting;

/// <summary>
/// Provides a display-only fallback for imported budget items that predate a direct vendor link.
/// It never writes or changes a budget item's vendor association.
/// </summary>
public static class LegacyBudgetItemVendorMatcher
{
    public sealed record VendorIdentity(Guid Id, string Code, string Name);

    public static Guid? MatchDescriptionPrefix(
        string? description,
        IEnumerable<VendorIdentity> vendors)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        // Legacy imports commonly use "Vendor code - Item description".
        // Require an explicit separator to avoid substring/brand-name false positives.
        var separator = description.IndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0) return null;
        var prefix = description[..separator].Trim();
        if (prefix.Length == 0) return null;

        var matchingIds = vendors
            .Where(vendor =>
                string.Equals(vendor.Code.Trim(), prefix, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vendor.Name.Trim(), prefix, StringComparison.OrdinalIgnoreCase))
            .Select(vendor => vendor.Id)
            .Distinct()
            .Take(2)
            .ToArray();

        // Ambiguous vendor names or codes must not display someone else's logo.
        return matchingIds.Length == 1 ? matchingIds[0] : null;
    }
}
