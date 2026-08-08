using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Budgeting;

public sealed record BudgetAmendmentOption(Guid Id, string Label);

public sealed record BudgetAmendmentSummary(
    Guid Id,
    string FiscalYear,
    string BudgetVersion,
    Guid BudgetItemId,
    string BudgetItem,
    decimal BaselineAtRead,
    decimal AmountDelta,
    decimal ProposedResult,
    string Reason,
    BudgetAmendmentState State,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAtUtc,
    string? DecisionBy,
    DateTimeOffset? DecisionAtUtc,
    string? DecisionNote,
    decimal? ResultingRevisedTotal);

public sealed record BudgetAmendmentSnapshot(
    IReadOnlyList<BudgetAmendmentSummary> Amendments,
    IReadOnlyList<BudgetAmendmentOption> EligibleBudgetItems);

public sealed class BudgetAmendmentService(LedgerForgeDbContext dbContext)
{
    public async Task<BudgetAmendmentSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var yearNames = await dbContext.FiscalYears.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var versionNames = await dbContext.BudgetVersions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => "v" + x.VersionNumber + " · " + x.Name, cancellationToken);
        var items = await dbContext.BudgetItems.AsNoTracking().ToDictionaryAsync(x => x.Id, cancellationToken);
        var amendments = await dbContext.BudgetAmendments.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        var summaries = amendments.Select(x =>
        {
            items.TryGetValue(x.BudgetItemId, out var item);
            var baseline = item is null ? 0m : item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
            return new BudgetAmendmentSummary(
                x.Id,
                yearNames.TryGetValue(x.FiscalYearId, out var year) ? year : "Unknown fiscal year",
                versionNames.TryGetValue(x.BudgetVersionId, out var version) ? version : "Unknown version",
                x.BudgetItemId,
                item is null ? "Unknown budget item" : item.ItemNumber + " · " + item.Description,
                baseline,
                x.AmountDelta,
                baseline + x.AmountDelta,
                x.Reason,
                x.State,
                x.SubmittedBy,
                x.SubmittedAtUtc,
                x.DecisionBy,
                x.DecisionAtUtc,
                x.DecisionNote,
                x.ResultingRevisedTotal);
        }).ToArray();

        return new(summaries, await EligibleItemsAsync(cancellationToken));
    }

    public async Task<Guid> CreateAsync(Guid budgetItemId, decimal amountDelta, string reason, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.BudgetItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == budgetItemId, cancellationToken)
            ?? throw new KeyNotFoundException("Budget item was not found.");
        if (item.Status is BudgetItemStatus.Cancelled or BudgetItemStatus.Archived)
            throw new InvalidOperationException("Cancelled or archived budget items cannot receive amendments.");
        var year = await dbContext.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == item.FiscalYearId, cancellationToken);
        if (year.IsLocked) throw new InvalidOperationException("Locked fiscal years cannot accept new amendments.");

        var amendment = new BudgetAmendment(item.FiscalYearId, item.BudgetVersionId, item.Id, amountDelta, reason);
        var baseline = item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
        if (baseline + amountDelta < 0m)
            throw new InvalidOperationException("Amendment would reduce the budget item below zero.");
        dbContext.BudgetAmendments.Add(amendment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return amendment.Id;
    }

    public async Task SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var amendment = await Require(id, cancellationToken);
        amendment.Submit(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var amendment = await Require(id, cancellationToken);
        var item = await dbContext.BudgetItems.SingleAsync(x => x.Id == amendment.BudgetItemId, cancellationToken);
        var year = await dbContext.FiscalYears.AsNoTracking().SingleAsync(x => x.Id == amendment.FiscalYearId, cancellationToken);
        if (year.IsLocked) throw new InvalidOperationException("Locked fiscal years cannot apply amendments.");
        var baseline = item.RevisedTotal ?? item.ApprovedTotal ?? item.PlannedTotal;
        var resulting = baseline + amendment.AmountDelta;
        if (resulting < 0m) throw new InvalidOperationException("Amendment would reduce the budget item below zero.");
        item.SetRevisedTotal(resulting);
        amendment.Approve(actor, resulting, note, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid id, string actor, string note, CancellationToken cancellationToken = default)
    {
        var amendment = await Require(id, cancellationToken);
        amendment.Reject(actor, note, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var amendment = await Require(id, cancellationToken);
        amendment.Cancel(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<BudgetAmendmentOption>> EligibleItemsAsync(CancellationToken cancellationToken)
    {
        var yearId = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.StartDate)
            .Where(x => !x.IsLocked)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (yearId is null) return [];
        var versionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == yearId.Value)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (versionId is null) return [];
        return await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.BudgetVersionId == versionId.Value && x.Status != BudgetItemStatus.Cancelled && x.Status != BudgetItemStatus.Archived)
            .OrderBy(x => x.ItemNumber)
            .Select(x => new BudgetAmendmentOption(
                x.Id,
                x.ItemNumber + " · " + x.Description + " · baseline " + (x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal)))
            .ToListAsync(cancellationToken);
    }

    private async Task<BudgetAmendment> Require(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Amendment ID is required.", nameof(id));
        return await dbContext.BudgetAmendments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Budget amendment was not found.");
    }
}
