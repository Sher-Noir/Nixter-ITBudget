namespace Crch.ItBudget.Infrastructure.Persistence.Seeding;

public sealed record LookupSeed(string Code, string Name, int SortOrder);
public sealed record NeedLevelSeed(string Code, string Name, int NumericValue, int SortOrder);
public sealed record FinanceAccountSeed(string Code, string Name, string FinanceCategoryCode, int SortOrder);

public static class WorkbookLookupSeeds
{
    public static IReadOnlyList<LookupSeed> BudgetSections { get; } =
    [
        new("CORE", "Core", 10),
        new("PROPOSED_HARDWARE", "Proposed Hardware", 20),
        new("PROPOSED_SOFTWARE", "Proposed Software", 30),
        new("DISASTER_RECOVERY", "Disaster Recovery", 40)
    ];

    public static IReadOnlyList<LookupSeed> FinanceTypes { get; } =
    [
        new("COMP_HARDWARE", "Comp. Hardware", 10),
        new("COMP_SOFTWARE", "Comp. Software", 20),
        new("CLINICAL_EQUIPMENT", "Clinical Equipment", 30),
        new("FURNITURE", "Furniture", 40),
        new("FIXTURE", "Fixture", 50),
        new("OFFICE_EQUIPMENT", "Office Equipment", 60),
        new("CONSULTING_PROJECT_HOURS", "Consulting/Project Hours", 70),
        new("LEASEHOLD_IMPROVEMENTS", "Leasehold Improvements", 80)
    ];

    public static IReadOnlyList<LookupSeed> FinanceCategories { get; } =
    [
        new("CONSULTANTS", "Consultants", 10),
        new("TEMPORARY_HELP", "Temporary Help", 20),
        new("OFFICE_SUPPLIES", "Office Supplies", 30),
        new("COMPUTER_SUPPLIES", "Computer Supplies", 40),
        new("EQUIPMENT_RENTAL", "Equipment Rental", 50),
        new("SERVICE_CONTRACTS", "Service Contracts", 60),
        new("SOFTWARE_MAINTENANCE", "Software Maintenance", 70),
        new("HARDWARE_MAINTENANCE", "Hardware Maintenance", 80),
        new("EQUIPMENT_REPAIRS", "Equipment Repairs", 90),
        new("MINOR_COMPUTER_EQUIPMENT", "Minor Computer Equipment", 100),
        new("TRAINING_CONFERENCES", "Training and Conferences", 110),
        new("EMPLOYEE_TRAVEL", "Employee Travel", 120),
        new("TELEPHONY", "Telephony", 130),
        new("INTERNET_CONNECTIVITY", "Internet / Connectivity", 140),
        new("MEAL_EXPENSE", "Meal Expense", 150),
        new("POSTAGE", "Postage", 160),
        new("LICENSES_PERMITS", "Licenses / Permits", 170),
        new("NOT_APPLICABLE", "Not Applicable", 180)
    ];

    public static IReadOnlyList<FinanceAccountSeed> FinanceAccounts { get; } =
    [
        new("00-5110-17-00", "Consultants", "CONSULTANTS", 10),
        new("00-5150-17-00", "Temporary Help", "TEMPORARY_HELP", 20),
        new("00-5310-17-00", "Office Supplies", "OFFICE_SUPPLIES", 30),
        new("00-5350-17-00", "Computer Supplies", "COMPUTER_SUPPLIES", 40),
        new("00-5550-17-00", "Equipment Rental", "EQUIPMENT_RENTAL", 50),
        new("00-5560-17-00", "Service Contracts", "SERVICE_CONTRACTS", 60),
        new("00-5561-17-00", "Software Maintenance", "SOFTWARE_MAINTENANCE", 70),
        new("00-5562-17-00", "Hardware Maintenance", "HARDWARE_MAINTENANCE", 80),
        new("00-5570-17-00", "Equipment Repairs", "EQUIPMENT_REPAIRS", 90),
        new("00-5582-17-00", "Minor Computer Equipment", "MINOR_COMPUTER_EQUIPMENT", 100),
        new("00-5600-17-00", "Training and Conferences", "TRAINING_CONFERENCES", 110),
        new("00-5610-17-00", "Employee Travel", "EMPLOYEE_TRAVEL", 120),
        new("00-5700-17-00", "Telephony", "TELEPHONY", 130),
        new("00-5705-17-00", "Internet / Connectivity", "INTERNET_CONNECTIVITY", 140),
        new("00-5805-17-00", "Meal Expense", "MEAL_EXPENSE", 150),
        new("00-5830-17-00", "Postage", "POSTAGE", 160),
        new("00-5880-17-00", "Licenses / Permits", "LICENSES_PERMITS", 170),
        new("00-0000-00-00", "Not Applicable", "NOT_APPLICABLE", 180)
    ];

    public static IReadOnlyList<LookupSeed> Departments { get; } =
    [
        new("01", "Medical", 10),
        new("02", "Dental", 20),
        new("03", "Behavioral Health", 30),
        new("06", "Population Health", 40),
        new("07", "Vision", 50),
        new("09", "Outreach", 60),
        new("10", "Family Planning", 70),
        new("12", "Health Benefits", 80),
        new("13", "Operations - Referrals", 90),
        new("15", "Finance", 100),
        new("16", "Billing", 110),
        new("17", "IT/IS", 120),
        new("18", "Operations Medical", 130),
        new("20", "Administration", 140),
        new("21", "Facility", 150),
        new("22", "Health Information", 160),
        new("23", "Development", 170),
        new("26", "Pharmacy", 180)
    ];

    public static IReadOnlyList<LookupSeed> Locations { get; } =
    [
        new("BR", "Brighton", 10),
        new("WA", "Waltham", 20),
        new("ALL", "All Locations", 30)
    ];

    public static IReadOnlyList<NeedLevelSeed> NeedLevels { get; } =
    [
        new("4", "Must Have", 4, 10),
        new("3", "High", 3, 20),
        new("2", "Moderate", 2, 30),
        new("1", "Low", 1, 40)
    ];

    public static IReadOnlyList<LookupSeed> Frequencies { get; } =
    [
        new("ONE_TIME", "One-Time", 10),
        new("MONTHLY", "Monthly", 20),
        new("QUARTERLY", "Quarterly", 30),
        new("ANNUALLY", "Annually", 40),
        new("OTHER_NOTE", "Other - See Note", 50),
        new("NOT_APPLICABLE", "Not Applicable", 60)
    ];

    public static IReadOnlyList<LookupSeed> Priorities { get; } =
    [
        new("HIGH", "High", 10),
        new("MEDIUM", "Medium", 20),
        new("LOW", "Low", 30),
        new("OTHER", "Other", 40)
    ];

    public static IReadOnlyList<LookupSeed> PurchaseTypes { get; } =
    [
        new("NEW", "New", 10),
        new("REPLACEMENT", "Replacement", 20),
        new("RENEWAL", "Renewal", 30),
        new("EXPANSION", "Expansion", 40),
        new("PROJECT", "Project", 50),
        new("OTHER", "Other", 60)
    ];

    public static IReadOnlyList<LookupSeed> InternalCategories { get; } =
    [
        new("HARDWARE", "Hardware", 10),
        new("SOFTWARE", "Software", 20),
        new("TELEPHONY", "Telephony", 30),
        new("OFFICE_SUPPLIES", "Office Supplies", 40),
        new("COMPUTER_SUPPLIES", "Computer Supplies", 50),
        new("EQUIPMENT_RENTAL", "Equipment Rental", 60),
        new("CONSULTANTS", "Consultants", 70),
        new("SERVICE_CONTRACTS", "Service Contracts", 80),
        new("SOFTWARE_MAINTENANCE", "Software Maintenance", 90),
        new("HARDWARE_MAINTENANCE", "Hardware Maintenance", 100),
        new("EQUIPMENT_REPAIRS", "Equipment Repairs", 110),
        new("TRAINING_CONFERENCES", "Training and Conferences", 120),
        new("TRAVEL", "Travel", 130),
        new("INTERNET_CONNECTIVITY", "Internet / Connectivity", 140),
        new("MEAL", "Meal", 150),
        new("POSTAGE", "Postage", 160),
        new("NOT_APPLICABLE", "Not Applicable", 170)
    ];
}
