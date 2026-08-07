using LedgerForge.Domain.MasterData;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.MasterData;

public sealed record FinanceCategoryOption(
    Guid Id,
    string Code,
    string Name,
    bool IsActive);

public sealed record FinanceAccountSummary(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid? FinanceCategoryId,
    string? FinanceCategoryCode,
    string? FinanceCategoryName);

public sealed record FinanceAdministrationSnapshot(
    IReadOnlyList<FinanceCategoryOption> Categories,
    IReadOnlyList<FinanceAccountSummary> Accounts);

public sealed class FinanceAdministrationService(LedgerForgeDbContext dbContext)
{
    public async Task<FinanceAdministrationSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.FinanceCategories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new FinanceCategoryOption(x.Id, x.Code, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

        var categoryById = categories.ToDictionary(x => x.Id);
        var accountRows = await dbContext.FinanceAccounts
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.SortOrder,
                x.IsActive,
                x.FinanceCategoryId
            })
            .ToListAsync(cancellationToken);

        var accounts = accountRows
            .Select(account =>
            {
                FinanceCategoryOption? category = null;
                if (account.FinanceCategoryId is Guid categoryId)
                    categoryById.TryGetValue(categoryId, out category);

                return new FinanceAccountSummary(
                    account.Id,
                    account.Code,
                    account.Name,
                    account.Description,
                    account.SortOrder,
                    account.IsActive,
                    account.FinanceCategoryId,
                    category?.Code,
                    category?.Name);
            })
            .ToArray();

        return new(categories, accounts);
    }

    public async Task AddAccountAsync(
        string code,
        string name,
        string? description,
        Guid? financeCategoryId,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountInput(code, name, description, sortOrder);
        code = code.Trim();
        name = name.Trim();
        financeCategoryId = NormalizeOptionalId(financeCategoryId, nameof(financeCategoryId));

        if (await dbContext.FinanceAccounts.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException($"Finance account code '{code}' already exists. Account codes are stable and must be unique.");

        if (financeCategoryId is not null)
            await RequireCategoryAsync(financeCategoryId.Value, requireActive: true, cancellationToken);

        var account = new FinanceAccount(code, name, financeCategoryId, sortOrder);
        account.UpdateDisplay(name, description, sortOrder);
        dbContext.FinanceAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAccountAsync(
        Guid id,
        string name,
        string? description,
        Guid? financeCategoryId,
        int sortOrder,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Finance account ID is required.", nameof(id));
        ValidateAccountInput("existing", name, description, sortOrder, validateCode: false);
        financeCategoryId = NormalizeOptionalId(financeCategoryId, nameof(financeCategoryId));

        var account = await dbContext.FinanceAccounts
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Finance account was not found.");

        if (financeCategoryId is not null)
            await RequireCategoryAsync(financeCategoryId.Value, requireActive: false, cancellationToken);

        account.UpdateDisplay(name, description, sortOrder);
        account.SetFinanceCategory(financeCategoryId);
        if (isActive) account.Activate();
        else account.Deactivate();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireCategoryAsync(Guid categoryId, bool requireActive, CancellationToken cancellationToken)
    {
        var category = await dbContext.FinanceCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
            ?? throw new ArgumentException("Selected finance category does not exist.", nameof(categoryId));

        if (requireActive && !category.IsActive)
            throw new InvalidOperationException("New finance accounts cannot be assigned to an inactive finance category.");
    }

    private static void ValidateAccountInput(string code, string name, string? description, int sortOrder, bool validateCode = true)
    {
        if (validateCode)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Finance account code is required.", nameof(code));
            if (code.Trim().Length > 100) throw new ArgumentException("Finance account code cannot exceed 100 characters.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Finance account name is required.", nameof(name));
        if (name.Trim().Length > 250) throw new ArgumentException("Finance account name cannot exceed 250 characters.", nameof(name));
        if (description?.Trim().Length > 1000) throw new ArgumentException("Finance account description cannot exceed 1000 characters.", nameof(description));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
    }

    private static Guid? NormalizeOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier cannot be an empty GUID.", parameterName);
        return value;
    }
}
