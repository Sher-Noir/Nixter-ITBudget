namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>Filter criteria shared by the Budget dashboard and its detailed CSV export.</summary>
public sealed record BudgetItemFilterOptions(
    string? Section, string? Category, string? Location,
    string? Vendor, string? Need, string? Status, string? Search);

public static class BudgetItemFilters
{
    public static BudgetItemSummary[] Apply(
        IEnumerable<BudgetItemSummary> items, BudgetItemFilterOptions filters)
    {
        return items
            .Where(x => string.IsNullOrWhiteSpace(filters.Section) ||
                string.Equals(x.BudgetSectionName, filters.Section, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Category) ||
                string.Equals(x.InternalCategoryName, filters.Category, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Location) ||
                string.Equals(x.LocationName, filters.Location, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Vendor) ||
                string.Equals(x.VendorName, filters.Vendor, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Need) ||
                string.Equals(x.NeedLevelName, filters.Need, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Status) ||
                string.Equals(x.Status.ToString(), filters.Status, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(filters.Search) ||
                x.ItemNumber.Contains(filters.Search, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(filters.Search, StringComparison.OrdinalIgnoreCase) ||
                (x.ReasonPurpose?.Contains(filters.Search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToArray();
    }
}
