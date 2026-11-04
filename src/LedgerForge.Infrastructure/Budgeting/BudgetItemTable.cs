using System.Globalization;
using LedgerForge.Domain.Budgeting;

namespace LedgerForge.Infrastructure.Budgeting;

/// <summary>
/// Sorts and pages the already-filtered Budget workspace rows. KPIs are calculated
/// from the full filtered set, not just the current page.
/// </summary>
public sealed record BudgetItemTablePage(
    IReadOnlyList<BudgetItemSummary> Rows,
    string SortKey,
    string Direction,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int FirstRow,
    int LastRow);

public static class BudgetItemTable
{
    public const int DefaultPageSize = 25;
    private static readonly int[] ValidPageSizes = [10, 25, 50, 100];

    public static BudgetItemTablePage Create(
        IEnumerable<BudgetItemSummary> filteredRows,
        string? sortKey,
        string? direction,
        int requestedPage,
        int requestedPageSize)
    {
        ArgumentNullException.ThrowIfNull(filteredRows);
        var rows = filteredRows.ToArray();
        var key = NormalizeSort(sortKey);
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        var size = ValidPageSizes.Contains(requestedPageSize) ? requestedPageSize : DefaultPageSize;

        var ordered = key switch
        {
            "description" => SortText(rows, x => x.Description, descending),
            "section" => SortText(rows, x => x.BudgetSectionName, descending),
            "category" => SortText(rows, x => x.InternalCategoryName ?? x.FinanceTypeName, descending),
            "vendor" => SortText(rows, x => x.VendorName, descending),
            "location" => SortText(rows, x => x.LocationName, descending),
            "quantity" => SortValue(rows, x => x.Quantity, descending),
            "unitCost" => SortValue(rows, x => x.UnitCost, descending),
            "total" => SortValue(rows, x => x.PlannedTotal, descending),
            "actual" => SortValue(rows, x => x.Actual, descending),
            "needLevel" => SortText(rows, x => x.NeedLevelName, descending),
            "frequency" => SortText(rows, x => x.FrequencyName, descending),
            "renewal" => SortValue(rows, x => x.RenewalDate, descending),
            "status" => SortText(rows, x => x.Status.ToString(), descending),
            "approval" => SortText(rows, x => ApprovalLabel(x.Status), descending),
            _ => SortValue(rows, x => NumericItemNumberKey(x.ItemNumber), descending)
        };

        // Deterministic ties prevent rows from jumping between pages across requests.
        var stableRows = ordered
            .ThenBy(x => NumericItemNumberKey(x.ItemNumber))
            .ThenBy(x => x.Id)
            .ToArray();
        var total = stableRows.Length;
        var pageCount = Math.Max(1, (total / size) + (total % size == 0 ? 0 : 1));
        var page = Math.Clamp(requestedPage, 1, pageCount);
        var offset = (page - 1) * size;
        return new(
            stableRows.Skip(offset).Take(size).ToArray(),
            key,
            descending ? "desc" : "asc",
            page,
            size,
            total,
            pageCount,
            total == 0 ? 0 : offset + 1,
            total == 0 ? 0 : Math.Min(offset + size, total));
    }

    private static string NormalizeSort(string? key) => key switch
    {
        "itemNumber" or "description" or "section" or "category" or "vendor" or
        "location" or "quantity" or "unitCost" or "total" or "actual" or "needLevel" or
        "frequency" or "renewal" or "status" or "approval" => key,
        _ => "itemNumber"
    };

    private static (int Kind, long Number, string Text) NumericItemNumberKey(string? value)
    {
        var normalized = (value ?? "").Trim();
        if (long.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return (0, number, normalized);
        return (1, 0, normalized.ToUpperInvariant());
    }

    private static string ApprovalLabel(BudgetItemStatus status) => status switch
    {
        BudgetItemStatus.Submitted => "Pending",
        BudgetItemStatus.Approved => "Approved",
        BudgetItemStatus.Denied => "Denied",
        BudgetItemStatus.Deferred => "Deferred",
        _ => "Not Started"
    };

    private static IOrderedEnumerable<BudgetItemSummary> SortText(
        IEnumerable<BudgetItemSummary> rows,
        Func<BudgetItemSummary, string?> selector,
        bool descending)
        => descending
            ? rows.OrderByDescending(x => selector(x) ?? "", StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(x => selector(x) ?? "", StringComparer.OrdinalIgnoreCase);

    private static IOrderedEnumerable<BudgetItemSummary> SortValue<TValue>(
        IEnumerable<BudgetItemSummary> rows,
        Func<BudgetItemSummary, TValue> selector,
        bool descending)
        => descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
}
