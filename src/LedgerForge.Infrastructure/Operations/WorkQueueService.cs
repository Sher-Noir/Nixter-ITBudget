using LedgerForge.Domain.Importing;
using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Operations;

public enum WorkItemCategory
{
    Approval,
    Renewal,
    ImportReview,
    FiscalClose
}

public sealed record WorkQueueItem(
    WorkItemCategory Category,
    string Title,
    string Detail,
    DateOnly? DueDate,
    string Url,
    decimal? Amount = null,
    bool IsUrgent = false);

public sealed record WorkQueueSnapshot(
    IReadOnlyList<WorkQueueItem> Items,
    int ApprovalCount,
    int RenewalCount,
    int ImportReviewCount,
    int FiscalCloseCount);

public sealed class WorkQueueService(
    LedgerForgeDbContext dbContext,
    ApprovalQueueService approvalQueueService,
    RenewalProjectionService renewalProjectionService,
    FiscalYearAdministrationService fiscalYearAdministrationService)
{
    public async Task<WorkQueueSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<WorkQueueItem>();
        var approvals = await approvalQueueService.GetAsync(cancellationToken);
        foreach (var approval in approvals.Items)
        {
            items.Add(new(
                WorkItemCategory.Approval,
                $"Approve {Label(approval.Type)} {approval.Reference}",
                $"{approval.FiscalYear} · {approval.Description}",
                approval.SubmittedAtUtc is null ? null : DateOnly.FromDateTime(approval.SubmittedAtUtc.Value.UtcDateTime),
                "/approvals",
                approval.Amount,
                IsUrgent: approval.SubmittedAtUtc < DateTimeOffset.UtcNow.AddDays(-7)));
        }

        var currentYear = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        var renewalCount = 0;
        var fiscalCloseCount = 0;
        if (currentYear is not null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cutoff = today.AddDays(90);
            var renewals = (await renewalProjectionService.GetForFiscalYearAsync(currentYear.Id, cancellationToken))
                .Where(x => x.RenewalDate <= cutoff)
                .OrderBy(x => x.RenewalDate)
                .ToArray();
            renewalCount = renewals.Length;
            foreach (var renewal in renewals)
            {
                var days = renewal.RenewalDate.DayNumber - today.DayNumber;
                items.Add(new(
                    WorkItemCategory.Renewal,
                    $"Renew {renewal.Reference}",
                    $"{renewal.Source} · {(renewal.Vendor is null ? renewal.Description : renewal.Vendor + " · " + renewal.Description)}",
                    renewal.RenewalDate,
                    renewal.ContractId is Guid contractId ? $"/contracts/{contractId}" : "/renewals",
                    renewal.EstimatedAmount,
                    IsUrgent: days <= 30));
            }

            if (currentYear.Status == Domain.Budgeting.FiscalYearStatus.Active && currentYear.EndDate <= today.AddDays(45))
            {
                var readiness = await fiscalYearAdministrationService.GetCloseReadinessAsync(currentYear.Id, cancellationToken);
                fiscalCloseCount = 1;
                items.Add(new(
                    WorkItemCategory.FiscalClose,
                    readiness.CanClose ? $"Close {currentYear.DisplayName}" : $"Prepare {currentYear.DisplayName} for close",
                    readiness.CanClose ? "All fiscal close checks currently pass." : string.Join(" ", readiness.Blockers.Take(3)),
                    currentYear.EndDate,
                    $"/fiscal-years/{currentYear.Id}",
                    readiness.OutstandingCommitment,
                    IsUrgent: currentYear.EndDate <= today.AddDays(15)));
            }
        }

        var importBatches = await dbContext.ImportBatches.AsNoTracking()
            .Where(x => x.Status == ImportBatchStatus.PreviewReady && x.AcceptedBy == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.SourceFileName, x.CreatedAtUtc, x.WarningCount, x.ErrorCount })
            .ToListAsync(cancellationToken);
        foreach (var batch in importBatches)
        {
            items.Add(new(
                WorkItemCategory.ImportReview,
                $"Review import {batch.SourceFileName}",
                $"{batch.ErrorCount} error(s), {batch.WarningCount} warning(s)",
                DateOnly.FromDateTime(batch.CreatedAtUtc.UtcDateTime),
                $"/imports/{batch.Id}",
                null,
                IsUrgent: batch.ErrorCount > 0 || batch.CreatedAtUtc < DateTimeOffset.UtcNow.AddDays(-3)));
        }

        var ordered = items
            .OrderByDescending(x => x.IsUrgent)
            .ThenBy(x => x.DueDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.Category)
            .ThenBy(x => x.Title)
            .ToArray();

        return new(ordered, approvals.TotalCount, renewalCount, importBatches.Count, fiscalCloseCount);
    }

    private static string Label(ApprovalQueueItemType type)
        => type switch
        {
            ApprovalQueueItemType.BudgetItem => "budget item",
            ApprovalQueueItemType.PurchaseOrder => "purchase order",
            ApprovalQueueItemType.Invoice => "invoice",
            ApprovalQueueItemType.BudgetAmendment => "budget amendment",
            _ => "item"
        };
}
