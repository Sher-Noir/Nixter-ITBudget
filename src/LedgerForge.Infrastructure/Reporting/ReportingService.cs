using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Reporting;

public sealed record ReportFiscalYearOption(Guid Id, string Name, bool IsCurrent);

public sealed record ReportCenterSnapshot(
    IReadOnlyList<ReportFiscalYearOption> FiscalYears,
    Guid? SelectedFiscalYearId,
    string FiscalYearName,
    string BudgetVersionName,
    decimal PlannedBudget,
    decimal ApprovedBudget,
    decimal RevisedBudget,
    decimal Committed,
    decimal Actual,
    decimal Available,
    decimal Forecast,
    bool ForecastIsPublished,
    int PendingApprovals,
    int RenewalsDueIn30Days,
    int BudgetItemCount,
    int PurchaseOrderCount,
    int ActualTransactionCount);

public sealed record BudgetExportRow(
    string FiscalYear,
    string Version,
    string ItemNumber,
    string Description,
    string Status,
    decimal Quantity,
    decimal UnitCost,
    decimal PlannedTotal,
    decimal? ApprovedTotal,
    decimal? RevisedTotal,
    DateOnly? EstimatedPurchaseDate,
    DateOnly? RenewalDate);

public sealed record ActualExportRow(
    string FiscalYear,
    DateOnly TransactionDate,
    string Kind,
    string Description,
    string? SourceReference,
    string? BudgetItem,
    string? FinanceAccount,
    string? Department,
    string? Location,
    string? FiscalPeriod,
    decimal Amount,
    string? ReversalReason);

public sealed record CommitmentExportRow(
    string FiscalYear,
    string PurchaseOrder,
    string Vendor,
    string State,
    decimal IssuedTotal,
    decimal PostedLinkedInvoices,
    decimal OutstandingCommitment);

public sealed record RenewalExportRow(
    string FiscalYear,
    string Source,
    string Reference,
    string Description,
    string? Vendor,
    DateOnly RenewalDate,
    DateOnly? NoticeDate,
    decimal EstimatedAmount,
    string Status);

public sealed record ForecastExportRow(
    string FiscalYear,
    string Scenario,
    DateOnly AsOfDate,
    string ItemNumber,
    string Description,
    decimal Baseline,
    decimal Forecast,
    decimal Variance,
    string? Note);

public sealed class ReportingService(
    LedgerForgeDbContext dbContext,
    OutstandingCommitmentService outstandingCommitmentService,
    ApprovalQueueService approvalQueueService,
    RenewalProjectionService renewalProjectionService,
    ForecastService forecastService)
{
    public async Task<ReportCenterSnapshot> GetCenterAsync(Guid? fiscalYearId, CancellationToken cancellationToken = default)
    {
        var years = await dbContext.FiscalYears.AsNoTracking()
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.StartDate)
            .Select(x => new ReportFiscalYearOption(x.Id, x.DisplayName, x.IsCurrent))
            .ToListAsync(cancellationToken);
        var selectedId = ResolveFiscalYear(years, fiscalYearId);
        if (selectedId is null)
            return new(years, null, "No fiscal year", "No budget version", 0m, 0m, 0m, 0m, 0m, 0m, 0m, false, 0, 0, 0, 0, 0);

        var yearName = years.Single(x => x.Id == selectedId.Value).Name;
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var actual = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var actualCount = await dbContext.ActualTransactions.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == selectedId.Value, cancellationToken);
        var committed = await outstandingCommitmentService.GetTotalForFiscalYearAsync(selectedId.Value, cancellationToken);
        var purchaseOrderCount = await dbContext.PurchaseOrders.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == selectedId.Value, cancellationToken);
        var pendingApprovals = (await approvalQueueService.GetAsync(selectedId.Value, cancellationToken)).TotalCount;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var renewalCutoff = today.AddDays(30);
        var renewalsDue = (await renewalProjectionService.GetForFiscalYearAsync(selectedId.Value, cancellationToken))
            .Count(x => x.RenewalDate >= today && x.RenewalDate <= renewalCutoff);

        if (version is null)
        {
            return new(
                years, selectedId, yearName, "No budget version",
                0m, 0m, 0m, committed, actual, -committed - actual,
                committed + actual, false, pendingApprovals, renewalsDue, 0, purchaseOrderCount, actualCount);
        }

        var items = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value && x.BudgetVersionId == version.Id)
            .Select(x => new { x.PlannedTotal, x.ApprovedTotal, x.RevisedTotal })
            .ToListAsync(cancellationToken);

        var planned = items.Sum(x => x.PlannedTotal);
        var approved = items.Sum(x => x.ApprovedTotal ?? 0m);
        var revised = items.Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal);
        var publishedForecast = await forecastService.GetLatestPublishedTotalAsync(selectedId.Value, version.Id, cancellationToken);

        return new(
            years,
            selectedId,
            yearName,
            version.Name,
            planned,
            approved,
            revised,
            committed,
            actual,
            revised - committed - actual,
            publishedForecast ?? Math.Max(revised, committed + actual),
            publishedForecast is not null,
            pendingApprovals,
            renewalsDue,
            items.Count,
            purchaseOrderCount,
            actualCount);
    }

    public async Task<IReadOnlyList<BudgetExportRow>> GetBudgetExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await RequireFiscalYearAsync(fiscalYearId, cancellationToken);
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null) return [];

        var rows = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == version.Id)
            .OrderBy(x => x.ItemNumber)
            .Select(x => new
            {
                x.ItemNumber, x.Description, x.Status, x.Quantity, x.UnitCost, x.PlannedTotal,
                x.ApprovedTotal, x.RevisedTotal, x.EstimatedPurchaseDate, x.RenewalDate
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new BudgetExportRow(
            year.DisplayName, version.Name, x.ItemNumber, x.Description, x.Status.ToString(), x.Quantity,
            x.UnitCost, x.PlannedTotal, x.ApprovedTotal, x.RevisedTotal, x.EstimatedPurchaseDate, x.RenewalDate)).ToArray();
    }

    public async Task<IReadOnlyList<ActualExportRow>> GetActualExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await RequireFiscalYearAsync(fiscalYearId, cancellationToken);
        var transactions = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var budgetNames = await dbContext.BudgetItems.AsNoTracking().Where(x => x.FiscalYearId == fiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var periodNames = await dbContext.FiscalPeriods.AsNoTracking().Where(x => x.FiscalYearId == fiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        return transactions.Select(x => new ActualExportRow(
            year.DisplayName, x.TransactionDate, x.Kind.ToString(), x.Description, x.SourceReference,
            NameFor(x.BudgetItemId, budgetNames), NameFor(x.FinanceAccountId, accountNames),
            NameFor(x.DepartmentId, departmentNames), NameFor(x.LocationId, locationNames),
            NameFor(x.FiscalPeriodId, periodNames), x.Amount, x.ReversalReason)).ToArray();
    }

    public async Task<IReadOnlyList<CommitmentExportRow>> GetCommitmentExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await RequireFiscalYearAsync(fiscalYearId, cancellationToken);
        var commitments = await outstandingCommitmentService.GetForFiscalYearAsync(fiscalYearId, cancellationToken);
        if (commitments.Count == 0) return [];
        var vendorIds = commitments.Select(x => x.VendorId).Distinct().ToArray();
        var vendorNames = await dbContext.Vendors.AsNoTracking()
            .Where(x => vendorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return commitments.Select(x => new CommitmentExportRow(
            year.DisplayName, x.PurchaseOrderNumber, vendorNames.GetValueOrDefault(x.VendorId) ?? "Unknown vendor",
            PurchaseOrderState.Issued.ToString(), x.IssuedTotal, x.PostedLinkedInvoiceTotal, x.OutstandingTotal)).ToArray();
    }

    public async Task<IReadOnlyList<RenewalExportRow>> GetRenewalExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await RequireFiscalYearAsync(fiscalYearId, cancellationToken);
        var rows = await renewalProjectionService.GetForFiscalYearAsync(fiscalYearId, cancellationToken);
        return rows.Select(x => new RenewalExportRow(
            year.DisplayName, x.Source, x.Reference, x.Description, x.Vendor, x.RenewalDate,
            x.NoticeDate, x.EstimatedAmount, x.Status)).ToArray();
    }

    public async Task<IReadOnlyList<ForecastExportRow>> GetForecastExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await RequireFiscalYearAsync(fiscalYearId, cancellationToken);
        var versionId = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (versionId is null) return [];
        var scenarioId = await forecastService.GetLatestPublishedScenarioIdAsync(fiscalYearId, versionId.Value, cancellationToken);
        if (scenarioId is null) return [];
        var detail = await forecastService.GetAsync(scenarioId.Value, cancellationToken);
        if (detail is null) return [];

        return detail.Lines.Select(x => new ForecastExportRow(
            year.DisplayName, detail.Scenario.Name, detail.Scenario.AsOfDate, x.ItemNumber, x.Description,
            x.Baseline, x.ForecastTotal, x.Variance, x.Note)).ToArray();
    }

    private async Task<FiscalYear> RequireFiscalYearAsync(Guid fiscalYearId, CancellationToken cancellationToken)
        => await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");

    private static Guid? ResolveFiscalYear(IReadOnlyList<ReportFiscalYearOption> years, Guid? requested)
    {
        if (requested is not null && years.Any(x => x.Id == requested)) return requested;
        return years.FirstOrDefault(x => x.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
    }

    private static string? NameFor(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is Guid value && names.TryGetValue(value, out var name) ? name : null;
}
