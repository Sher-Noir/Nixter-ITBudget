using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.MasterData;

public sealed record LookupTypeDefinition(string Key, string DisplayName, bool HasNumericValue = false);

public sealed record ManagedLookupSummary(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    int? NumericValue = null);

public sealed record LookupAdministrationSnapshot(
    IReadOnlyList<LookupTypeDefinition> Types,
    string SelectedType,
    IReadOnlyList<ManagedLookupSummary> Values);

public sealed class LookupAdministrationService(LedgerForgeDbContext dbContext)
{
    public static IReadOnlyList<LookupTypeDefinition> Types { get; } =
    [
        new("BudgetSection", "Budget Sections"),
        new("FinanceType", "Finance Types"),
        new("FinanceCategory", "Finance Categories"),
        new("Department", "Departments"),
        new("Location", "Locations"),
        new("NeedLevel", "Need Levels", true),
        new("InternalCategory", "Internal Categories"),
        new("Frequency", "Frequencies"),
        new("Priority", "Priorities"),
        new("PurchaseType", "Purchase Types"),
        new("UnitOfMeasure", "Units of Measure"),
        new("DocumentType", "Document Types"),
        new("ApprovalStatus", "Approval Statuses"),
        new("TransactionType", "Transaction Types"),
        new("PurchaseOrderStatus", "Purchase Order Statuses"),
        new("InvoiceStatus", "Invoice Statuses"),
        new("RenewalStatus", "Renewal Statuses"),
        new("ContractStatus", "Contract Statuses")
    ];

    public async Task<LookupAdministrationSnapshot> GetAsync(
        string? type,
        CancellationToken cancellationToken = default)
    {
        var selectedType = ResolveType(type);
        var values = await ListAsync(selectedType, cancellationToken);
        return new(Types, selectedType, values);
    }

    public async Task AddAsync(
        string type,
        string code,
        string name,
        string? description,
        int sortOrder,
        int? numericValue,
        CancellationToken cancellationToken = default)
    {
        ValidateCreate(code, name, sortOrder);
        var selectedType = ResolveType(type);
        code = code.Trim();
        name = name.Trim();

        switch (selectedType)
        {
            case "BudgetSection": await AddCommonAsync(dbContext.BudgetSections, code, name, description, sortOrder, x => new BudgetSection(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "FinanceType": await AddCommonAsync(dbContext.FinanceTypes, code, name, description, sortOrder, x => new FinanceType(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "FinanceCategory": await AddCommonAsync(dbContext.FinanceCategories, code, name, description, sortOrder, x => new FinanceCategory(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "Department": await AddCommonAsync(dbContext.Departments, code, name, description, sortOrder, x => new Department(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "Location": await AddCommonAsync(dbContext.Locations, code, name, description, sortOrder, x => new Location(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "InternalCategory": await AddCommonAsync(dbContext.InternalCategories, code, name, description, sortOrder, x => new InternalCategory(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "Frequency": await AddCommonAsync(dbContext.Frequencies, code, name, description, sortOrder, x => new Frequency(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "Priority": await AddCommonAsync(dbContext.Priorities, code, name, description, sortOrder, x => new PriorityLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "PurchaseType": await AddCommonAsync(dbContext.PurchaseTypes, code, name, description, sortOrder, x => new PurchaseTypeLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "UnitOfMeasure": await AddCommonAsync(dbContext.UnitsOfMeasure, code, name, description, sortOrder, x => new UnitOfMeasure(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "DocumentType": await AddCommonAsync(dbContext.DocumentTypes, code, name, description, sortOrder, x => new DocumentType(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "ApprovalStatus": await AddCommonAsync(dbContext.ApprovalStatuses, code, name, description, sortOrder, x => new ApprovalStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "TransactionType": await AddCommonAsync(dbContext.TransactionTypes, code, name, description, sortOrder, x => new TransactionTypeLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "PurchaseOrderStatus": await AddCommonAsync(dbContext.PurchaseOrderStatuses, code, name, description, sortOrder, x => new PurchaseOrderStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "InvoiceStatus": await AddCommonAsync(dbContext.InvoiceStatuses, code, name, description, sortOrder, x => new InvoiceStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "RenewalStatus": await AddCommonAsync(dbContext.RenewalStatuses, code, name, description, sortOrder, x => new RenewalStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "ContractStatus": await AddCommonAsync(dbContext.ContractStatuses, code, name, description, sortOrder, x => new ContractStatusLookup(x.Code, x.Name, x.SortOrder), cancellationToken); break;
            case "NeedLevel":
                var rank = numericValue ?? throw new ArgumentException("Need level numeric rank is required.", nameof(numericValue));
                if (rank < 0) throw new ArgumentOutOfRangeException(nameof(numericValue));
                if (await dbContext.NeedLevels.AnyAsync(x => x.Code == code, cancellationToken)) throw DuplicateCode(code);
                var needLevel = new NeedLevel(code, name, rank, sortOrder);
                needLevel.UpdateDisplay(name, description, sortOrder);
                dbContext.NeedLevels.Add(needLevel);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        string type,
        Guid id,
        string name,
        string? description,
        int sortOrder,
        int? numericValue,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Lookup ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Lookup name is required.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        var selectedType = ResolveType(type);

        switch (selectedType)
        {
            case "BudgetSection": await UpdateCommonAsync(dbContext.BudgetSections, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "FinanceType": await UpdateCommonAsync(dbContext.FinanceTypes, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "FinanceCategory": await UpdateCommonAsync(dbContext.FinanceCategories, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "Department": await UpdateCommonAsync(dbContext.Departments, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "Location": await UpdateCommonAsync(dbContext.Locations, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "InternalCategory": await UpdateCommonAsync(dbContext.InternalCategories, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "Frequency": await UpdateCommonAsync(dbContext.Frequencies, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "Priority": await UpdateCommonAsync(dbContext.Priorities, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "PurchaseType": await UpdateCommonAsync(dbContext.PurchaseTypes, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "UnitOfMeasure": await UpdateCommonAsync(dbContext.UnitsOfMeasure, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "DocumentType": await UpdateCommonAsync(dbContext.DocumentTypes, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "ApprovalStatus": await UpdateCommonAsync(dbContext.ApprovalStatuses, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "TransactionType": await UpdateCommonAsync(dbContext.TransactionTypes, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "PurchaseOrderStatus": await UpdateCommonAsync(dbContext.PurchaseOrderStatuses, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "InvoiceStatus": await UpdateCommonAsync(dbContext.InvoiceStatuses, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "RenewalStatus": await UpdateCommonAsync(dbContext.RenewalStatuses, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "ContractStatus": await UpdateCommonAsync(dbContext.ContractStatuses, id, name, description, sortOrder, isActive, cancellationToken); break;
            case "NeedLevel":
                var needLevel = await dbContext.NeedLevels.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw NotFound();
                needLevel.UpdateDisplay(name, description, sortOrder);
                needLevel.SetNumericValue(numericValue ?? throw new ArgumentException("Need level numeric rank is required.", nameof(numericValue)));
                SetState(needLevel, isActive);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ManagedLookupSummary>> ListAsync(string type, CancellationToken cancellationToken)
    {
        return type switch
        {
            "BudgetSection" => await ListCommonAsync(dbContext.BudgetSections, cancellationToken),
            "FinanceType" => await ListCommonAsync(dbContext.FinanceTypes, cancellationToken),
            "FinanceCategory" => await ListCommonAsync(dbContext.FinanceCategories, cancellationToken),
            "Department" => await ListCommonAsync(dbContext.Departments, cancellationToken),
            "Location" => await ListCommonAsync(dbContext.Locations, cancellationToken),
            "InternalCategory" => await ListCommonAsync(dbContext.InternalCategories, cancellationToken),
            "Frequency" => await ListCommonAsync(dbContext.Frequencies, cancellationToken),
            "Priority" => await ListCommonAsync(dbContext.Priorities, cancellationToken),
            "PurchaseType" => await ListCommonAsync(dbContext.PurchaseTypes, cancellationToken),
            "UnitOfMeasure" => await ListCommonAsync(dbContext.UnitsOfMeasure, cancellationToken),
            "DocumentType" => await ListCommonAsync(dbContext.DocumentTypes, cancellationToken),
            "ApprovalStatus" => await ListCommonAsync(dbContext.ApprovalStatuses, cancellationToken),
            "TransactionType" => await ListCommonAsync(dbContext.TransactionTypes, cancellationToken),
            "PurchaseOrderStatus" => await ListCommonAsync(dbContext.PurchaseOrderStatuses, cancellationToken),
            "InvoiceStatus" => await ListCommonAsync(dbContext.InvoiceStatuses, cancellationToken),
            "RenewalStatus" => await ListCommonAsync(dbContext.RenewalStatuses, cancellationToken),
            "ContractStatus" => await ListCommonAsync(dbContext.ContractStatuses, cancellationToken),
            "NeedLevel" => await dbContext.NeedLevels.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ManagedLookupSummary(x.Id, x.Code, x.Name, x.Description, x.SortOrder, x.IsActive, x.NumericValue)).ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static async Task<IReadOnlyList<ManagedLookupSummary>> ListCommonAsync<TEntity>(DbSet<TEntity> set, CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
        => await set.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ManagedLookupSummary(x.Id, x.Code, x.Name, x.Description, x.SortOrder, x.IsActive)).ToListAsync(cancellationToken);

    private static async Task AddCommonAsync<TEntity>(
        DbSet<TEntity> set,
        string code,
        string name,
        string? description,
        int sortOrder,
        Func<(string Code, string Name, int SortOrder), TEntity> factory,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (await set.AnyAsync(x => x.Code == code, cancellationToken)) throw DuplicateCode(code);
        var entity = factory((code, name, sortOrder));
        entity.UpdateDisplay(name, description, sortOrder);
        set.Add(entity);
    }

    private static async Task UpdateCommonAsync<TEntity>(
        DbSet<TEntity> set,
        Guid id,
        string name,
        string? description,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        var entity = await set.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw NotFound();
        entity.UpdateDisplay(name, description, sortOrder);
        SetState(entity, isActive);
    }

    private static void SetState(ManagedLookupEntity entity, bool isActive)
    {
        if (isActive) entity.Activate();
        else entity.Deactivate();
    }

    private static string ResolveType(string? type)
    {
        var candidate = string.IsNullOrWhiteSpace(type) ? Types[0].Key : type.Trim();
        return Types.FirstOrDefault(x => string.Equals(x.Key, candidate, StringComparison.OrdinalIgnoreCase))?.Key
            ?? throw new ArgumentException("Unknown lookup type.", nameof(type));
    }

    private static void ValidateCreate(string code, string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Lookup code is required.", nameof(code));
        if (code.Trim().Length > 100) throw new ArgumentException("Lookup code cannot exceed 100 characters.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Lookup name is required.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
    }

    private static InvalidOperationException DuplicateCode(string code) => new($"Lookup code '{code}' already exists. Codes are stable and must be unique.");
    private static KeyNotFoundException NotFound() => new("Lookup value was not found.");
}
