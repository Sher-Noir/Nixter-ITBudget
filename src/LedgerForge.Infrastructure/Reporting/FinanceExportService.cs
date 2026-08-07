using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Reporting;

public sealed record FinancePostingExportRow(
    DateOnly TransactionDate,
    string FiscalYear,
    string? FiscalPeriod,
    string Kind,
    string Description,
    string? SourceReference,
    string? FinanceAccount,
    string? Department,
    string? Location,
    string? BudgetItem,
    decimal Amount);

public sealed class FinanceExportService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<FinancePostingExportRow>> GetAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        var rows = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.TransactionDate, x.Kind, x.Description, x.SourceReference, x.FinanceAccountId,
                x.DepartmentId, x.LocationId, x.BudgetItemId, x.FiscalPeriodId, x.Amount
            })
            .ToListAsync(cancellationToken);

        var accountIds = rows.Where(x => x.FinanceAccountId != null).Select(x => x.FinanceAccountId!.Value).Distinct().ToArray();
        var departmentIds = rows.Where(x => x.DepartmentId != null).Select(x => x.DepartmentId!.Value).Distinct().ToArray();
        var locationIds = rows.Where(x => x.LocationId != null).Select(x => x.LocationId!.Value).Distinct().ToArray();
        var itemIds = rows.Where(x => x.BudgetItemId != null).Select(x => x.BudgetItemId!.Value).Distinct().ToArray();
        var periodIds = rows.Where(x => x.FiscalPeriodId != null).Select(x => x.FiscalPeriodId!.Value).Distinct().ToArray();

        var accounts = accountIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.FinanceAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var departments = departmentIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Departments.AsNoTracking().Where(x => departmentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var locations = locationIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.Locations.AsNoTracking().Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var items = itemIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.BudgetItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ItemNumber, cancellationToken);
        var periods = periodIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.FiscalPeriods.AsNoTracking().Where(x => periodIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        return rows.Select(x => new FinancePostingExportRow(
            x.TransactionDate,
            year.DisplayName,
            Value(x.FiscalPeriodId, periods),
            x.Kind.ToString(),
            x.Description,
            x.SourceReference,
            Value(x.FinanceAccountId, accounts),
            Value(x.DepartmentId, departments),
            Value(x.LocationId, locations),
            Value(x.BudgetItemId, items),
            x.Amount)).ToArray();
    }

    private static string? Value(Guid? id, IReadOnlyDictionary<Guid, string> values)
        => id is Guid value && values.TryGetValue(value, out var result) ? result : null;
}
