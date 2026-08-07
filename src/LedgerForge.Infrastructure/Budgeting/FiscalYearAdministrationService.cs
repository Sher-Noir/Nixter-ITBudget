using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record FiscalYearSummary(
    Guid Id,
    string DisplayName,
    DateOnly StartDate,
    DateOnly EndDate,
    int PlanningYear,
    FiscalYearStatus Status,
    bool IsCurrent,
    bool IsLocked,
    int PeriodCount,
    int VersionCount);

public sealed record BudgetVersionSummary(
    Guid Id,
    Guid FiscalYearId,
    string Name,
    BudgetVersionType VersionType,
    int VersionNumber,
    DateOnly? EffectiveDate,
    bool IsLocked,
    DateTimeOffset? ApprovedAtUtc);

public sealed class FiscalYearAdministrationService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<FiscalYearSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.FiscalYears
            .AsNoTracking()
            .OrderByDescending(x => x.StartDate)
            .Select(x => new FiscalYearSummary(
                x.Id,
                x.DisplayName,
                x.StartDate,
                x.EndDate,
                x.PlanningYear,
                x.Status,
                x.IsCurrent,
                x.LockedAtUtc != null,
                dbContext.FiscalPeriods.Count(period => period.FiscalYearId == x.Id),
                dbContext.BudgetVersions.Count(version => version.FiscalYearId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetVersionSummary>> ListVersionsAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BudgetVersions
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.VersionNumber)
            .Select(x => new BudgetVersionSummary(
                x.Id,
                x.FiscalYearId,
                x.Name,
                x.VersionType,
                x.VersionNumber,
                x.EffectiveDate,
                x.IsLocked,
                x.ApprovedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        string displayName,
        DateOnly startDate,
        DateOnly endDate,
        int planningYear,
        string? description,
        bool isCurrent,
        bool createMonthlyPeriods,
        string initialVersionName,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.FiscalYears.AnyAsync(x => x.DisplayName == displayName.Trim(), cancellationToken))
            throw new InvalidOperationException("A fiscal year with this display name already exists.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (isCurrent)
        {
            var existingCurrent = await dbContext.FiscalYears
                .Where(x => x.IsCurrent)
                .ToListAsync(cancellationToken);
            foreach (var existing in existingCurrent) existing.SetCurrent(false);
        }

        var fiscalYear = new FiscalYear(displayName, startDate, endDate, planningYear);
        fiscalYear.UpdateDetails(displayName, startDate, endDate, planningYear, description);
        fiscalYear.SetCurrent(isCurrent);
        fiscalYear.SetStatus(FiscalYearStatus.Planning);
        dbContext.FiscalYears.Add(fiscalYear);

        if (createMonthlyPeriods)
        {
            foreach (var period in BuildMonthlyPeriods(fiscalYear.Id, startDate, endDate))
                dbContext.FiscalPeriods.Add(period);
        }

        var initialVersion = new BudgetVersion(
            fiscalYear.Id,
            string.IsNullOrWhiteSpace(initialVersionName) ? "Initial Planning" : initialVersionName,
            BudgetVersionType.InitialPlanning,
            versionNumber: 1,
            effectiveDate: startDate);
        dbContext.BudgetVersions.Add(initialVersion);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return fiscalYear.Id;
    }

    public async Task SetCurrentAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year ID is required.", nameof(fiscalYearId));

        var years = await dbContext.FiscalYears.ToListAsync(cancellationToken);
        var selected = years.SingleOrDefault(x => x.Id == fiscalYearId)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");

        foreach (var year in years) year.SetCurrent(year.Id == selected.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<FiscalPeriod> BuildMonthlyPeriods(
        Guid fiscalYearId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var periods = new List<FiscalPeriod>();
        var cursor = startDate;
        var periodNumber = 1;

        while (cursor <= endDate)
        {
            var monthEnd = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            var periodEnd = monthEnd < endDate ? monthEnd : endDate;
            periods.Add(new FiscalPeriod(
                fiscalYearId,
                periodNumber,
                $"P{periodNumber:00}",
                cursor.ToString("MMM yyyy"),
                cursor,
                periodEnd));

            if (periodEnd == endDate) break;
            cursor = periodEnd.AddDays(1);
            periodNumber++;
        }

        return periods;
    }
}
