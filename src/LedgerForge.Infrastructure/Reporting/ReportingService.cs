using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
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
    string ItemNumber,
    string Description,
    DateOnly RenewalDate,
    decimal EstimatedAmount,
    string Status);

public sealed class ReportingService(
    LedgerForgeDbContext dbContext,
    OutstandingCommitmentService outstandingCommitmentService)
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
            return new(years, null, "No fiscal year", "No budget version", 0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 0, 0, 0);

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
        var pendingPurchaseOrders = await dbContext.PurchaseOrders.AsNoTracking()
            .CountAsync(x => x.FiscalYearId == selectedId.Value && x.State == PurchaseOrderState.PendingApproval, cancellationToken);

        if (version is null)
        {
            return new(
                years, selectedId, yearName, "No budget version",
                0m, 0m, 0m, committed, actual, -committed - actual,
                pendingPurchaseOrders, 0, 0, purchaseOrderCount, actualCount);
        }

        var items = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == selectedId.Value && x.BudgetVersionId == version.Id)
            .Select(x => new
            {
                x.PlannedTotal,
                x.ApprovedTotal,
                x.RevisedTotal,
                x.Status,
                x.RenewalDate
            })
            .ToListAsync(cancellationToken);

        var planned = items.Sum(x => x.PlannedTotal);
        var approved = items.Sum(x => x.ApprovedTotal ?? 0m);
        var revised = items.Sum(x => x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var renewalCutoff = today.AddDays(30);

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
            items.Count(x => x.Status == BudgetItemStatus.Submitted) + pendingPurchaseOrders,
            items.Count(x => x.RenewalDate is not null && x.RenewalDate.Value >= today && x.RenewalDate.Value <= renewalCutoff),
            items.Count,
            purchaseOrderCount,
            actualCount);
    }

    public async Task<IReadOnlyList<BudgetExportRow>> GetBudgetExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
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
                x.ItemNumber,
                x.Description,
                x.Status,
                x.Quantity,
                x.UnitCost,
                x.PlannedTotal,
                x.ApprovedTotal,
                x.RevisedTotal,
                x.EstimatedPurchaseDate,
                x.RenewalDate
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new BudgetExportRow(
            year.DisplayName,
            version.Name,
            x.ItemNumber,
            x.Description,
            x.Status.ToString(),
            x.Quantity,
            x.UnitCost,
            x.PlannedTotal,
            x.ApprovedTotal,
            x.RevisedTotal,
            x.EstimatedPurchaseDate,
            x.RenewalDate)).ToArray();
    }

    public async Task<IReadOnlyList<ActualExportRow>> GetActualExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        var transactions = await dbContext.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var budgetNames = await dbContext.BudgetItems.AsNoTracking().Where(x => x.FiscalYearId == fiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.ItemNumber + " · " + x.Description, cancellationToken);
        var accountNames = await dbContext.FinanceAccounts.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var departmentNames = await dbContext.Departments.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var locationNames = await dbContext.Locations.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);
        var periodNames = await dbContext.FiscalPeriods.AsNoTracking().Where(x => x.FiscalYearId == fiscalYearId)
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, cancellationToken);

        return transactions.Select(x => new ActualExportRow(
            year.DisplayName,
            x.TransactionDate,
            x.Kind.ToString(),
            x.Description,
            x.SourceReference,
            NameFor(x.BudgetItemId, budgetNames),
            NameFor(x.FinanceAccountId, accountNames),
            NameFor(x.DepartmentId, departmentNames),
            NameFor(x.LocationId, locationNames),
            NameFor(x.FiscalPeriodId, periodNames),
            x.Amount,
            x.ReversalReason)).ToArray();
    }

    public async Task<IReadOnlyList<CommitmentExportRow>> GetCommitmentExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        var commitments = await outstandingCommitmentService.GetForFiscalYearAsync(fiscalYearId, cancellationToken);
        if (commitments.Count == 0) return [];

        var vendorIds = commitments.Select(x => x.VendorId).Distinct().ToArray();
        var vendorNames = await dbContext.Vendors.AsNoTracking()
            .Where(x => vendorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return commitments.Select(commitment => new CommitmentExportRow(
            year.DisplayName,
            commitment.PurchaseOrderNumber,
            vendorNames.TryGetValue(commitment.VendorId, out var vendor) ? vendor : "Unknown vendor",
            PurchaseOrderState.Issued.ToString(),
            commitment.IssuedTotal,
            commitment.PostedLinkedInvoiceTotal,
            commitment.OutstandingTotal)).ToArray();
    }

    public async Task<IReadOnlyList<RenewalExportRow>> GetRenewalExportAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new KeyNotFoundException("Fiscal year was not found.");
        var version = await dbContext.BudgetVersions.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId)
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null) return [];

        var rows = await dbContext.BudgetItems.AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.BudgetVersionId == version.Id && x.RenewalDate != null)
            .OrderBy(x => x.RenewalDate)
            .Select(x => new
            {
                x.ItemNumber,
                x.Description,
                x.RenewalDate,
                x.RevisedTotal,
                x.ApprovedTotal,
                x.PlannedTotal,
                x.Status
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new RenewalExportRow(
            year.DisplayName,
            x.ItemNumber,
            x.Description,
            x.RenewalDate!.Value,
            x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal,
            x.Status.ToString())).ToArray();
    }

    private static Guid? ResolveFiscalYear(IReadOnlyList<ReportFiscalYearOption> years, Guid? requested)
    {
        if (requested is not null && years.Any(x => x.Id == requested)) return requested;
        return years.FirstOrDefault(x => x.IsCurrent)?.Id ?? years.FirstOrDefault()?.Id;
    }

    private static string? NameFor(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is Guid value && names.TryGetValue(value, out var name) ? name : null;
}
