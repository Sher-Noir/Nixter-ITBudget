namespace LedgerForge.Infrastructure.Persistence.Seeding;

public sealed record LookupSeed(string Code, string Name, int SortOrder);
public sealed record NeedLevelSeed(string Code, string Name, int NumericValue, int SortOrder);

/// <summary>
/// Generic defaults that are safe to ship in the public LedgerForge repository.
/// Organization-specific departments, locations, finance accounts, account categories,
/// and finance mappings are intentionally not seeded here.
/// </summary>
public static class DefaultLookupSeeds
{
    public static IReadOnlyList<LookupSeed> BudgetSections { get; } =
    [
        new("CORE", "Core", 10),
        new("PROJECTS", "Projects", 20),
        new("PROPOSED", "Proposed", 30)
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
        new("MEALS", "Meals", 150),
        new("POSTAGE", "Postage", 160),
        new("OTHER", "Other", 170),
        new("NOT_APPLICABLE", "Not Applicable", 180)
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
        new("OTHER", "Other", 50),
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
}
