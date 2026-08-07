using LedgerForge.Infrastructure.Reporting;
using LedgerForge.Web.Exporting;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewBudget)]
[Route("exports")]
public sealed class ExportsController(ReportingService reportingService) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => RedirectToAction("Index", "Reports");

    [HttpGet("budget.csv")]
    public async Task<IActionResult> Budget(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetBudgetExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Budget Version", "Item Number", "Description", "Status", "Quantity", "Unit Cost", "Planned Total", "Approved Total", "Revised Total", "Estimated Purchase Date", "Renewal Date"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.Version, x.ItemNumber, x.Description, x.Status, x.Quantity, x.UnitCost,
                    x.PlannedTotal, x.ApprovedTotal, x.RevisedTotal, x.EstimatedPurchaseDate, x.RenewalDate
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-budget.csv");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("renewals.csv")]
    public async Task<IActionResult> Renewals(Guid fiscalYearId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await reportingService.GetRenewalExportAsync(fiscalYearId, cancellationToken);
            var bytes = CsvExportWriter.Write(
                ["Fiscal Year", "Item Number", "Description", "Renewal Date", "Estimated Amount", "Status"],
                rows.Select(x => (IReadOnlyList<object?>)new object?[]
                {
                    x.FiscalYear, x.ItemNumber, x.Description, x.RenewalDate, x.EstimatedAmount, x.Status
                }));
            return File(bytes, "text/csv; charset=utf-8", "ledgerforge-renewals.csv");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
