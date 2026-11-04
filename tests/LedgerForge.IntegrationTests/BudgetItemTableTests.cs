using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetItemTableTests
{
    [Fact]
    public void DefaultSort_OrdersItemNumbersNumerically()
    {
        var result = BudgetItemTable.Create(
            [Row("10"), Row("2"), Row("1")], null, null, 1, 25);

        Assert.Equal(new[] { "1", "2", "10" }, result.Rows.Select(x => x.ItemNumber).ToArray());
        Assert.Equal("itemNumber", result.SortKey);
        Assert.Equal("asc", result.Direction);
    }

    [Fact]
    public void DescendingMoneySort_UsesNumericValuesNotFormattedText()
    {
        var result = BudgetItemTable.Create(
            [Row("1", total: 9m), Row("2", total: 100m), Row("3", total: 25m)],
            "total", "desc", 1, 25);

        Assert.Equal(new[] { 100m, 25m, 9m }, result.Rows.Select(x => x.PlannedTotal).ToArray());
        Assert.Equal("desc", result.Direction);
    }

    [Fact]
    public void PageBoundaries_AreCorrect_AndRowsDoNotRepeat()
    {
        var rows = Enumerable.Range(1, 33)
            .Select(i => Row(i.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        var first = BudgetItemTable.Create(rows, "itemNumber", "asc", 1, 10);
        var second = BudgetItemTable.Create(rows, "itemNumber", "asc", 2, 10);
        var last = BudgetItemTable.Create(rows, "itemNumber", "asc", 4, 10);

        Assert.Equal(4, first.TotalPages);
        Assert.Equal((1, 10), (first.FirstRow, first.LastRow));
        Assert.Equal((11, 20), (second.FirstRow, second.LastRow));
        Assert.Equal((31, 33), (last.FirstRow, last.LastRow));
        Assert.Equal(10, first.Rows.Count);
        Assert.Equal(10, second.Rows.Count);
        Assert.Equal(3, last.Rows.Count);
        Assert.Empty(first.Rows.Select(x => x.Id).Intersect(second.Rows.Select(x => x.Id)));
    }

    [Fact]
    public void PageAndPageSizeAreClampedToSupportedValues()
    {
        var rows = Enumerable.Range(1, 31).Select(i => Row(i.ToString())).ToArray();
        var invalid = BudgetItemTable.Create(rows, "unknown", "anything", -400, 9999);
        var tooHigh = BudgetItemTable.Create(rows, "description", "desc", int.MaxValue, 10);

        Assert.Equal(25, invalid.PageSize);
        Assert.Equal(1, invalid.PageNumber);
        Assert.Equal(2, invalid.TotalPages);
        Assert.Equal("itemNumber", invalid.SortKey);
        Assert.Equal("asc", invalid.Direction);
        Assert.Equal(4, tooHigh.PageNumber);
        Assert.Equal(31, tooHigh.FirstRow);
        Assert.Equal(31, tooHigh.LastRow);
    }

    [Fact]
    public void EmptySearchResults_DisplayZeroRowsOnFirstPage()
    {
        var empty = BudgetItemTable.Create([], "vendor", "asc", 500, 50);
        Assert.Empty(empty.Rows);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.TotalPages);
        Assert.Equal(1, empty.PageNumber);
        Assert.Equal(0, empty.FirstRow);
        Assert.Equal(0, empty.LastRow);
    }

    [Fact]
    public void FilteringBeforePaging_UsesOnlyMatchingItems()
    {
        var rows = new[]
        {
            Row("1", vendor: "Vendor A"),
            Row("2", vendor: "Vendor B"),
            Row("3", vendor: "Vendor A")
        };
        var filtered = rows.Where(x => x.VendorName == "Vendor A");
        var result = BudgetItemTable.Create(filtered, "itemNumber", "desc", 1, 25);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { "3", "1" }, result.Rows.Select(x => x.ItemNumber).ToArray());
    }

    [Theory]
    [InlineData("itemNumber")]
    [InlineData("description")]
    [InlineData("section")]
    [InlineData("category")]
    [InlineData("vendor")]
    [InlineData("location")]
    [InlineData("quantity")]
    [InlineData("unitCost")]
    [InlineData("total")]
    [InlineData("needLevel")]
    [InlineData("frequency")]
    [InlineData("renewal")]
    [InlineData("status")]
    [InlineData("approval")]
    public void EveryBudgetColumnIsSortable(string key)
    {
        var result = BudgetItemTable.Create([Row("1"), Row("2")], key, "asc", 1, 25);
        Assert.Equal(key, result.SortKey);
        Assert.Equal(2, result.Rows.Count);
    }

    private static BudgetItemSummary Row(
        string number,
        decimal total = 10m,
        string? vendor = null)
        => new(
            Id: Guid.NewGuid(),
            StableIdentifier: "LF-" + number,
            ItemNumber: number,
            Description: "Item " + number,
            ReasonPurpose: null,
            PurchaseType: PurchaseType.New,
            Quantity: 1m,
            UnitCost: total,
            PlannedTotal: total,
            ApprovedTotal: null,
            RevisedTotal: null,
            EstimatedPurchaseDate: null,
            RenewalDate: null,
            Status: BudgetItemStatus.Draft,
            BudgetSectionId: null,
            BudgetSectionName: "Operations",
            FinanceTypeId: null,
            FinanceTypeName: null,
            DepartmentId: null,
            DepartmentName: null,
            LocationId: null,
            LocationName: null,
            NeedLevelId: null,
            NeedLevelName: null,
            InternalCategoryId: null,
            InternalCategoryName: null,
            FrequencyId: null,
            FrequencyName: null,
            VendorName: vendor,
            VendorId: null,
            Notes: null);
}
