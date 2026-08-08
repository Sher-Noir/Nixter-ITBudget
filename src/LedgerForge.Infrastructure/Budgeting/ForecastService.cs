using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record ForecastFiscalYearOption(Guid Id, string Name, bool IsCurrent);

public sealed record ForecastScenarioSummary(
    Guid Id,
    string FiscalYear,
    string BudgetVersion,
    string Name,
    DateOnly AsOfDate,
    ForecastScenarioState State,
    int LineCount,
    decimal ForecastTotal,
    string? PublishedBy,
    DateTimeOffset? PublishedAtUtc);

public sealed record ForecastLineSummary(
    Guid Id,
    Guid BudgetItemId,
    string ItemNumber,
    string Description,
    decimal Baseline,
    decimal ForecastTotal,
    decimal Variance,
    string? Note);

public sealed record ForecastIndexSnapshot(
    IReadOnlyList<ForecastScenarioSummary> Scenarios,
    IReadOnlyList<ForecastFiscalYearOption> FiscalYears);

public sealed record ForecastDetailSnapshot(
    ForecastScenarioSummary Scenario,
    string? Notes,
    IReadOnlyList<ForecastLineSummary> Lines,
    decimal BaselineTotal,
    decimal ForecastTotal,
    decimal Variance);

public sealed class ForecastService(LedgerForgeDbContext dbContext)
{
    public async Task<ForecastIndexSnapshot> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new ForecastFiscalYearOption(x.Id, x.DisplayName, x.IsCurrent))
            .ToListAsync(cancellationToken);
        var yearNames = years.ToDictionary(x => x.Id, x => x.Name);
        var versionNames = await dbContext.BudgetVersions.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => "v" + x.VersionNumber + " · " + x.Name, cancellationToken);
        var totals = await dbContext.ForecastLines.AsNoTracking()
            .GroupBy(x => x.ForecastScenarioId)
            .Select(x => new { Id = x.Key, Count = x.Count(), Total = x.Sum(line => line.ForecastTotal) })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var scenarios = await dbContext.ForecastScenarios.AsNoTracking()
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new(
            scenarios.Select(x => new ForecastScenarioSummary(
                x.Id,
                yearNames.TryGetValue(x.FiscalYearId, out var year) ? year : "Unknown fiscal year",
                versionNames.TryGetValue(x.BudgetVersionId, out var version) ? version : "Unknown version",
                x.Name,
                x.AsOfDate,
                x.State,
                totals.TryGetValue(x.Id, out var total) ? total.Count : 0,
                totals.TryGetValue(x.Id, out total) ? total.Total : 0m,
                x.PublishedBy,
                x.PublishedAtUtc)).ToArray(),
            years);
    }

    public async Task<ForecastDetailSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scenario = await dbContext.ForecastScenarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (scenario is null) return null;
        var year = await dbContext.FiscalYears.AsNoTracking().Where(x => x.Id == scenario.FiscalYearId).Select(x => x.DisplayName).SingleAsync(cancellationToken);
        var version = await dbContext.BudgetVersions.AsNoTracking().SingleAsync(x => x.Id == scenario.BudgetVersionId, cancellationToken);
        var lines = await dbContext.ForecastLines.AsNoTracking().Where(x => x.ForecastScenarioId == id).ToListAsync(cancellationToken);
        var itemIds = lines.Select(x => x.BudgetItemId).Distinct().ToArray();
        var items = itemIds.Length == 0
            ? new Dictionary<Guid, BudgetItem>()
            : await dbContext.BudgetItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        var summaries = lines.Select(line =>
        {
            items.TryGetValue(line.BudgetItemId, out var item);
            return new ForecastLineSummary(
                line.Id,
                line.BudgetItemId,
                item?.ItemNumber ?? "Unknown",
                item?.Description ?? "Budget item unavailable",
                line.BaselineTotal,
                line.ForecastTotal,
                line.ForecastTotal - line.BaselineTotal,
                line.Note);
        }).OrderBy(x => x.ItemNumber).ThenBy(x => x.Description).ToArray();

        var summary = new ForecastScenarioSummary(
            scenario.Id,
            year,
            "v" + version.VersionNumber + " · " + version.Name,
            scenario.Name,
            scenario.AsOfDate,
            scenario.State,
            summaries.Length,
            summaries.Sum(x => x.ForecastTotal),
            scenario.PublishedBy,
            scenario.PublishedAtUtc);
        var baselineTotal = summaries.Sum(x => x.Baseline);
        var forecastTotal = summaries.Sum(x => x.ForecastTotal);
        return new(summary, scenario.Notes, summaries, baselineTotal, forecastTotal, forecastTotal - baselineTotal);
    }

    public async Task<Guid> CreateAsync(
        Guid fiscalYearId,
        string name,
        DateOnly asOfDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        if (asOfDate < year.StartDate || asOfDate > year.EndDate)
            throw new InvalidOperationException("Forecast as-of date must fall within the selected fiscal year.");
        name = name?.Trim() ?? string.Empty;
        if (await dbContext.ForecastScenarios.AnyAsync(x => x.FiscalYearId == fiscalYearId && x.Name == name, cancellationToken))
            throw new InvalidOperationException($"Forecast scenario '{name}' already exists in {year.DisplayName}.");

        var version = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The selected fiscal year does not have a budget version to forecast.");
        var items = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == version.Id &&
                        x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived)
            .OrderBy(x => x.ItemNumber)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
            throw new InvalidOperationException("The selected budget version has no eligible items to forecast.");

        var scenario = new ForecastScenario(fiscalYearId, version.Id, name, asOfDate, notes);
        dbContext.ForecastScenarios.Add(scenario);
        foreach (var item in items)
        {
            var baseline = item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
            dbContext.ForecastLines.Add(new ForecastLine(scenario.Id, item.Id, baseline, baseline));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return scenario.Id;
    }

    public async Task UpdateLineAsync(
        Guid scenarioId,
        Guid lineId,
        decimal forecastTotal,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var scenario = await RequireScenario(scenarioId, cancellationToken);
        if (scenario.State != ForecastScenarioState.Draft)
            throw new InvalidOperationException("Only draft forecast scenarios can be edited.");
        var line = await dbContext.ForecastLines.SingleOrDefaultAsync(x => x.Id == lineId && x.ForecastScenarioId == scenarioId, cancellationToken)
            ?? throw new KeyNotFoundException("Forecast line was not found in the selected scenario.");
        line.Update(forecastTotal, note);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireScenario(id, cancellationToken);
        if (!await dbContext.ForecastLines.AsNoTracking().AnyAsync(x => x.ForecastScenarioId == id, cancellationToken))
            throw new InvalidOperationException("A forecast scenario must contain at least one line before publication.");
        scenario.Publish(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireScenario(id, cancellationToken);
        scenario.Archive(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal?> GetLatestPublishedTotalAsync(
        Guid fiscalYearId,
        Guid budgetVersionId,
        CancellationToken cancellationToken = default)
    {
        var scenarioId = await GetLatestPublishedScenarioIdAsync(fiscalYearId, budgetVersionId, cancellationToken);
        if (scenarioId is null) return null;
        return await dbContext.ForecastLines.AsNoTracking()
            .Where(x => x.ForecastScenarioId == scenarioId.Value)
            .SumAsync(x => (decimal?)x.ForecastTotal, cancellationToken) ?? 0m;
    }

    public async Task<Guid?> GetLatestPublishedScenarioIdAsync(
        Guid fiscalYearId,
        Guid budgetVersionId,
        CancellationToken cancellationToken = default)
        => await dbContext.ForecastScenarios.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == budgetVersionId && x.State == ForecastScenarioState.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.AsOfDate)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ForecastScenario> RequireScenario(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Forecast scenario ID is required.", nameof(id));
        return await dbContext.ForecastScenarios.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Forecast scenario was not found.");
    }
}
