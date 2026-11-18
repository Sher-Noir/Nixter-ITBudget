using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Web.Exporting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("exports")]
public sealed class ExportsController(
    ReportingService reportingService,
    BudgetPlanningService planningService,
    FinanceExportService financeExportService,
    IConfiguration configuration) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction("Index", "Reports");

    [HttpGet("budget.csv")]
    public async Task<IActionResult> Budget(
        Guid fiscalYearId, Guid? versionId,
        string? section, string? category, string? location, string? vendor,
        string? need, string? status, string? q,
        CancellationToken cancellationToken)
    {
        if (fiscalYearId == Guid.Empty) return NotFound();
        var snapshot = await planningService.GetSnapshotAsync(
            fiscalYearId, versionId, cancellationToken);

        // Never silently export a different fiscal year or version than requested.
        if (snapshot.SelectedFiscalYearId != fiscalYearId ||
            (versionId.HasValue && snapshot.SelectedVersionId != versionId))
            return NotFound();

        var fiscal = snapshot.FiscalYears.Single(x => x.Id == fiscalYearId);
        var version = snapshot.Versions.FirstOrDefault(x => x.Id == snapshot.SelectedVersionId);
        var items = BudgetItemFilters.Apply(snapshot.Items,
            new BudgetItemFilterOptions(section, category, location, vendor, need, status, q));
        var asOf = RecurringActualService.EasternToday();

        var headers = new[]
        {
            // Preserve the original twelve budget-export columns.
            "Fiscal Year", "Budget Version", "Item Number", "Description", "Status",
            "Quantity", "Unit Cost", "Planned Total", "Approved Total", "Revised Total",
            "Estimated Purchase Date", "Renewal Date",
            // Identification, grouping and source assumptions.
            "Budget Item Id", "Vendor", "Budget Section", "Internal Category",
            "Finance Type", "Department", "Location", "Need Level", "Frequency",
            "Purchase Type", "Reason Purpose", "Notes",
            "As Of Date (Eastern)", "Fiscal Year Start", "Fiscal Year End",
            // Dashboard reconciliation: signed numeric over/under.
            "Current Budget", "Current Actual", "Current Forecasted (Full Year)",
            "Expected To Date", "Current Over Under (Expected Minus Actual)",
            "Current Budget Minus Actual", "Forecast Minus Current Budget",
            // Audit of where Expected To Date came from.
            "Expected Recurring To Date",
            "Expected Dated Purchases To Date",
            "Expected Undated Spent Or Purchased To Date",
            "Expected Undated Fiscal Pacing To Date",
            "Expected To Date Basis",
            // Annual forecast assumptions.
            "Remaining Unposted Recurring", "Outstanding PO Commitments",
            "Additional PO Commitments Beyond Recurring",
            "Published Forecast Scenario Total", "Effective Forecast Floor",
            "Additional Expected From Forecast Floor",
            "Recurring Schedules (Description | Monthly Amount | Billing Day | Dates | Status)",
            "Billing Cadence Setting", "Effective Billing Cadence",
            "Recorded Billing Periods", "Per-Period Budget",
            "Expected Billing Installments To Date",
            "Remaining Year Billing Allowance", "Billing Cadence Basis"
        };

        object?[] ItemRow(BudgetItemSummary x) =>
        [
            fiscal.DisplayName, version?.Name, x.ItemNumber, x.Description, x.Status.ToString(),
            x.Quantity, x.UnitCost, x.PlannedTotal, x.ApprovedTotal, x.RevisedTotal,
            x.EstimatedPurchaseDate, x.RenewalDate,
            x.Id, x.VendorName, x.BudgetSectionName, x.InternalCategoryName,
            x.FinanceTypeName, x.DepartmentName, x.LocationName, x.NeedLevelName,
            x.FrequencyName, x.PurchaseType.ToString(), x.ReasonPurpose, x.Notes,
            asOf, fiscal.StartDate, fiscal.EndDate,
            x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal,
            x.Actual, x.Forecast, x.ExpectedToDate, x.ExpectedToDate - x.Actual,
            (x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal) - x.Actual,
            x.Forecast - (x.RevisedTotal ?? x.ApprovedTotal ?? x.PlannedTotal),
            x.ExpectedRecurringToDate,
            x.ExpectedDatedPurchaseToDate,
            x.ExpectedUndatedSpentToDate,
            x.ExpectedUndatedPacedToDate,
            x.ExpectedToDateBasis,
            x.UnpostedRecurring, x.OutstandingCommitments,
            x.AdditionalUncoveredCommitments, x.PublishedScenarioTotal,
            x.ForecastFloor, x.AdditionalExpectedFromForecastFloor,
            x.RecurringScheduleDetails,
            x.BillingCadence.ToString(), x.EffectiveBillingCadence.ToString(),
            x.BilledPeriods, x.BudgetPerBillingPeriod,
            x.ExpectedInstallmentsToDate, x.RemainingInstallmentAllowance,
            x.BillingCadenceBasis
        ];

        // All numeric totals use the exact same filtered items as the Budget KPIs.
        // A total row allows Excel users to reconcile with the dashboard instantly.
        var totals = new object?[headers.Length];
        totals[0] = fiscal.DisplayName;
        totals[1] = version?.Name;
        totals[2] = $"TOTAL ({items.Length} filtered items)";
        totals[24] = asOf;
        totals[25] = fiscal.StartDate;
        totals[26] = fiscal.EndDate;
        var itemRows = items.Select(ItemRow).ToArray();
        foreach (var index in new[]
        {
            7, 8, 9, 27, 28, 29, 30, 31, 32, 33,
            34, 35, 36, 37, 39, 40, 41, 42, 43, 44,
            49, 50, 51
        })
        {
            totals[index] = itemRows.Sum(row =>
                row[index] is decimal number ? number : 0m);
        }
        totals[38] = "TOTAL: Expected To Date minus Current Actual (negative = over expected pace)";
        // Published scenario is optional per item; retain the raw sum for reconciliation.
        var csvRows = itemRows
            .Select(row => (IReadOnlyList<object?>)row)
            .Append(totals)
            .ToArray();
        var bytes = CsvExportWriter.Write(headers, csvRows);
        return File(bytes, "text/csv; charset=utf-8",
            "ledgerforge-budget-detailed-" + asOf.ToString("yyyyMMdd") + ".csv");
    }

    [HttpGet("actuals.csv")]
    public async Task<IActionResult> Actuals(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetActualExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Transaction Date", "Kind", "Description", "Source Reference", "Budget Item", "Finance Account", "Department", "Location", "Fiscal Period", "Amount", "Reversal Reason"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.TransactionDate, x.Kind, x.Description, x.SourceReference, x.BudgetItem,
                    x.FinanceAccount, x.Department, x.Location, x.FiscalPeriod, x.Amount, x.ReversalReason
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-actuals.csv");
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("finance.csv")]
    public async Task<IActionResult> Finance(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await financeExportService.GetAsync(fiscalYearId, cancellationToken);
            string H(string key, string fallback) => configuration[$"FinanceExport:Headers:{key}"]?.Trim() is { Length: > 0 } value ? value : fallback;
            var bytes = CsvExportWriter.Write(
                [H("TransactionDate", "Transaction Date"), H("FiscalYear", "Fiscal Year"), H("FiscalPeriod", "Fiscal Period"), H("Kind", "Kind"), H("Description", "Description"), H("SourceReference", "Source Reference"), H("FinanceAccount", "Finance Account"), H("Department", "Department"), H("Location", "Location"), H("BudgetItem", "Budget Item"), H("Amount", "Amount")],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.TransactionDate, x.FiscalYear, x.FiscalPeriod, x.Kind, x.Description,
                    x.SourceReference, x.FinanceAccount, x.Department, x.Location, x.BudgetItem, x.Amount
                }));
            var fileName = configuration["FinanceExport:FileName"]?.Trim();
            return File(bytes, "text/csv; charset=utf-8", string.IsNullOrWhiteSpace(fileName) ? "ledgerforge-finance-posting.csv" : Path.GetFileName(fileName));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("commitments.csv")]
    public async Task<IActionResult> Commitments(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetCommitmentExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Purchase Order", "Vendor", "State", "Issued PO Total", "Posted Linked Invoices", "Outstanding Commitment"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.PurchaseOrder, x.Vendor, x.State, x.IssuedTotal,
                    x.PostedLinkedInvoices, x.OutstandingCommitment
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-open-commitments.csv");
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("renewals.csv")]
    public async Task<IActionResult> Renewals(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetRenewalExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Source", "Reference", "Description", "Vendor", "Renewal Date", "Notice Date", "Estimated Amount", "Status"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.Source, x.Reference, x.Description, x.Vendor, x.RenewalDate,
                    x.NoticeDate, x.EstimatedAmount, x.Status
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-renewals.csv");
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("forecast.csv")]
    public async Task<IActionResult> Forecast(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetForecastExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Scenario", "As Of Date", "Item Number", "Description", "Baseline", "Forecast", "Variance", "Note"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.Scenario, x.AsOfDate, x.ItemNumber, x.Description,
                    x.Baseline, x.Forecast, x.Variance, x.Note
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-forecast.csv");
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
