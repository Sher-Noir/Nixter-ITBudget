using LedgerForge.Application.Procurement;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Procurement;

public sealed record OutstandingCommitment(
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid VendorId,
    decimal IssuedTotal,
    decimal PostedLinkedInvoiceTotal,
    decimal OutstandingTotal);

public sealed class OutstandingCommitmentService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<OutstandingCommitment>> GetForFiscalYearAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var orders = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYearId && x.State == PurchaseOrderState.Issued)
            .Select(x => new { x.Id, x.Number, x.VendorId })
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0) return [];

        var orderIds = orders.Select(x => x.Id).ToArray();
        var grossTotals = await dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.PurchaseOrderId))
            .GroupBy(x => x.PurchaseOrderId)
            .Select(group => new { PurchaseOrderId = group.Key, Total = group.Sum(x => x.LineTotal) })
            .ToDictionaryAsync(x => x.PurchaseOrderId, x => x.Total, cancellationToken);

        var postedInvoiceTotals = await dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.PurchaseOrderId != null && orderIds.Contains(x.PurchaseOrderId.Value) && x.State == InvoiceState.Posted)
            .GroupBy(x => x.PurchaseOrderId!.Value)
            .Select(group => new { PurchaseOrderId = group.Key, Total = group.Sum(x => x.TotalAmount) })
            .ToDictionaryAsync(x => x.PurchaseOrderId, x => x.Total, cancellationToken);

        return orders.Select(order =>
        {
            var gross = grossTotals.GetValueOrDefault(order.Id);
            var posted = postedInvoiceTotals.GetValueOrDefault(order.Id);
            return new OutstandingCommitment(
                order.Id,
                order.Number,
                order.VendorId,
                gross,
                posted,
                OutstandingCommitmentCalculator.Calculate(gross, posted));
        }).ToArray();
    }

    public async Task<decimal> GetTotalForFiscalYearAsync(
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
        => (await GetForFiscalYearAsync(fiscalYearId, cancellationToken)).Sum(x => x.OutstandingTotal);
}
