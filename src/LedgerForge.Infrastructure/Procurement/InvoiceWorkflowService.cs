using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Procurement;

public sealed class InvoiceWorkflowService(LedgerForgeDbContext dbContext)
{
    public async Task<Guid> CreateAsync(
        Guid fiscalYearId,
        Guid vendorId,
        string invoiceNumber,
        DateOnly invoiceDate,
        string description,
        decimal totalAmount,
        Guid? purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var year = await dbContext.FiscalYears.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fiscalYearId, cancellationToken)
            ?? throw new ArgumentException("Selected fiscal year does not exist.", nameof(fiscalYearId));
        if (invoiceDate < year.StartDate || invoiceDate > year.EndDate)
            throw new InvalidOperationException("Invoice date must fall within the selected fiscal year.");
        if (!await dbContext.Vendors.AsNoTracking().AnyAsync(x => x.Id == vendorId && x.IsActive, cancellationToken))
            throw new ArgumentException("Selected vendor does not exist or is inactive.", nameof(vendorId));

        if (purchaseOrderId is not null)
            await ValidateLinkedPurchaseOrder(fiscalYearId, vendorId, purchaseOrderId.Value, cancellationToken);

        invoiceNumber = invoiceNumber?.Trim() ?? string.Empty;
        if (await dbContext.Invoices.AnyAsync(x => x.FiscalYearId == fiscalYearId && x.VendorId == vendorId && x.InvoiceNumber == invoiceNumber, cancellationToken))
            throw new InvalidOperationException($"Invoice number '{invoiceNumber}' already exists for this vendor in the selected fiscal year.");

        var invoice = new Invoice(fiscalYearId, vendorId, invoiceNumber, invoiceDate, description, totalAmount, purchaseOrderId);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task AddAllocationAsync(
        Guid invoiceId,
        string description,
        decimal amount,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(invoiceId, cancellationToken);
        if (invoice.State != InvoiceState.Draft)
            throw new InvalidOperationException("Allocations can only be added to draft invoices.");

        await ValidateDimensions(invoice, budgetItemId, financeAccountId, departmentId, locationId, cancellationToken);
        if (fiscalPeriodId is not null)
            await RequireInvoicePeriod(invoice, fiscalPeriodId.Value, requireOpen: false, cancellationToken);

        var allocated = await AllocationTotal(invoiceId, cancellationToken);
        if (allocated + amount > invoice.TotalAmount)
            throw new InvalidOperationException("Invoice allocations cannot exceed the invoice total.");
        var lastLine = await dbContext.InvoiceAllocations.Where(x => x.InvoiceId == invoiceId)
            .MaxAsync(x => (int?)x.LineNumber, cancellationToken) ?? 0;

        dbContext.InvoiceAllocations.Add(new InvoiceAllocation(
            invoiceId, lastLine + 1, description, amount,
            budgetItemId, financeAccountId, departmentId, locationId, fiscalPeriodId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(id, cancellationToken);
        var allocated = await AllocationTotal(id, cancellationToken);
        if (allocated != invoice.TotalAmount)
            throw new InvalidOperationException($"Invoice allocations must equal the invoice total before submission. Allocated {allocated:0.00} of {invoice.TotalAmount:0.00}.");
        invoice.Submit(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(id, cancellationToken);
        invoice.Approve(actor, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(id, cancellationToken);
        invoice.Reject(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(id, cancellationToken);
        invoice.Cancel(actor, reason, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitToLedgerAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireInvoice(id, cancellationToken);
        if (invoice.State != InvoiceState.Approved)
            throw new InvalidOperationException($"Invoice cannot be posted from state {invoice.State}.");
        if (await dbContext.ActualTransactions.AnyAsync(x => x.InvoiceId == id, cancellationToken))
            throw new InvalidOperationException("This invoice already has posted actual transactions.");

        var allocations = await dbContext.InvoiceAllocations
            .Where(x => x.InvoiceId == id).OrderBy(x => x.LineNumber).ToListAsync(cancellationToken);
        if (allocations.Count == 0 || allocations.Sum(x => x.Amount) != invoice.TotalAmount)
            throw new InvalidOperationException("Invoice allocations must equal the invoice total before posting.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var allocation in allocations)
            {
                var period = allocation.FiscalPeriodId is Guid periodId
                    ? await RequireInvoicePeriod(invoice, periodId, requireOpen: true, cancellationToken)
                    : await ResolveOpenPeriod(invoice, cancellationToken);
                if (allocation.FiscalPeriodId is null && period is not null)
                    allocation.AssignFiscalPeriod(period.Id);

                dbContext.ActualTransactions.Add(new ActualTransaction(
                    invoice.FiscalYearId,
                    invoice.InvoiceDate,
                    allocation.Amount,
                    allocation.Description,
                    ActualTransactionKind.Invoice,
                    invoice.InvoiceNumber,
                    allocation.BudgetItemId,
                    allocation.FinanceAccountId,
                    allocation.DepartmentId,
                    allocation.LocationId,
                    period?.Id,
                    invoice.Id));
            }

            invoice.MarkPosted(actor, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ValidateLinkedPurchaseOrder(Guid fiscalYearId, Guid vendorId, Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("Purchase order ID cannot be empty.", nameof(purchaseOrderId));
        var order = await dbContext.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == purchaseOrderId, cancellationToken)
            ?? throw new ArgumentException("Selected purchase order does not exist.", nameof(purchaseOrderId));
        if (order.FiscalYearId != fiscalYearId || order.VendorId != vendorId)
            throw new InvalidOperationException("Selected purchase order must belong to the same fiscal year and vendor as the invoice.");
        if (order.State is not (PurchaseOrderState.Issued or PurchaseOrderState.Closed))
            throw new InvalidOperationException("Invoices can only be linked to issued or closed purchase orders.");
    }

    private async Task ValidateDimensions(
        Invoice invoice,
        Guid? budgetItemId,
        Guid? financeAccountId,
        Guid? departmentId,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        if (budgetItemId is not null)
        {
            if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item ID cannot be empty.", nameof(budgetItemId));
            if (!await dbContext.BudgetItems.AsNoTracking().AnyAsync(x => x.Id == budgetItemId && x.FiscalYearId == invoice.FiscalYearId, cancellationToken))
                throw new ArgumentException("Selected budget item does not belong to the invoice fiscal year.", nameof(budgetItemId));
        }
        await RequireActiveLookup(dbContext.FinanceAccounts, financeAccountId, "finance account", cancellationToken);
        await RequireActiveLookup(dbContext.Departments, departmentId, "department", cancellationToken);
        await RequireActiveLookup(dbContext.Locations, locationId, "location", cancellationToken);
    }

    private async Task<FiscalPeriod?> ResolveOpenPeriod(Invoice invoice, CancellationToken cancellationToken)
    {
        var periods = await dbContext.FiscalPeriods
            .Where(x => x.FiscalYearId == invoice.FiscalYearId && x.StartDate <= invoice.InvoiceDate && x.EndDate >= invoice.InvoiceDate)
            .ToListAsync(cancellationToken);
        if (periods.Count > 1) throw new InvalidOperationException("More than one fiscal period contains the invoice date.");
        var period = periods.SingleOrDefault();
        if (period?.IsClosed == true) throw new InvalidOperationException($"Fiscal period {period.Code} is closed and cannot accept invoice posting.");
        return period;
    }

    private async Task<FiscalPeriod> RequireInvoicePeriod(Invoice invoice, Guid fiscalPeriodId, bool requireOpen, CancellationToken cancellationToken)
    {
        if (fiscalPeriodId == Guid.Empty) throw new ArgumentException("Fiscal period ID cannot be empty.", nameof(fiscalPeriodId));
        var period = await dbContext.FiscalPeriods.SingleOrDefaultAsync(x => x.Id == fiscalPeriodId, cancellationToken)
            ?? throw new ArgumentException("Selected fiscal period does not exist.", nameof(fiscalPeriodId));
        if (period.FiscalYearId != invoice.FiscalYearId)
            throw new InvalidOperationException("Selected fiscal period does not belong to the invoice fiscal year.");
        if (invoice.InvoiceDate < period.StartDate || invoice.InvoiceDate > period.EndDate)
            throw new InvalidOperationException("Invoice date does not fall within the selected fiscal period.");
        if (requireOpen && period.IsClosed)
            throw new InvalidOperationException($"Fiscal period {period.Code} is closed and cannot accept invoice posting.");
        return period;
    }

    private async Task<Invoice> RequireInvoice(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invoice ID is required.", nameof(id));
        return await dbContext.Invoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Invoice was not found.");
    }

    private async Task<decimal> AllocationTotal(Guid id, CancellationToken cancellationToken)
        => await dbContext.InvoiceAllocations.Where(x => x.InvoiceId == id)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    private static async Task RequireActiveLookup<TEntity>(DbSet<TEntity> set, Guid? id, string label, CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        if (id is null) return;
        if (id == Guid.Empty) throw new ArgumentException($"Selected {label} ID cannot be empty.", nameof(id));
        if (!await set.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, cancellationToken))
            throw new InvalidOperationException($"Selected {label} does not exist or is inactive.");
    }
}
