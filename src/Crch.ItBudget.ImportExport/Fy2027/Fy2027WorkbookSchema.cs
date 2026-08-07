namespace Crch.ItBudget.ImportExport.Fy2027;

public static class Fy2027WorkbookSchema
{
    public const string BudgetSummarySheet = "Budget Summary";
    public const string FinanceCopySheet = "Finance Copy";
    public const string IndividualCategoryLookupSheet = "Individual Category Lookup";
    public const string RenewalCalendarSheet = "Renewal Calendar";
    public const string RawBudgetDetailSheet = "Raw Budget Detail";
    public const string RawBudgetInfoSheet = "Raw Budget Info";
    public const string ListsSheet = "Lists";
    public const string BackendSheet = "Backend";

    public static IReadOnlyList<string> RequiredSheets { get; } =
    [
        BudgetSummarySheet,
        FinanceCopySheet,
        IndividualCategoryLookupSheet,
        RenewalCalendarSheet,
        RawBudgetDetailSheet,
        RawBudgetInfoSheet,
        ListsSheet,
        BackendSheet
    ];

    public static IReadOnlyList<string> RawBudgetInfoHeaders { get; } =
    [
        "Item",
        "Item Description",
        "Reason / Purpose",
        "Estimated Month",
        "Type",
        "Department",
        "Location",
        "# of Units",
        "Estimated Cost / Unit",
        "Estimated Cost",
        "Need Level",
        "Notes",
        "Internal Category",
        "Frequency",
        "Renewal Date",
        "Vendor",
        "Status",
        "Approval",
        "Denial",
        "Proposed"
    ];

    public static IReadOnlyList<string> ListsHeaders { get; } =
    [
        "Budget Section",
        "Account Code",
        "Account Category",
        "Frequency",
        "Priority",
        "Status",
        "Reason / Purpose",
        "Month",
        "Type",
        "Department",
        "Location",
        "Need Level",
        "Internal Category"
    ];
}
