using LedgerForge.Domain.MasterData;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.MasterData;

public sealed record ConfigurationDeleteResult(bool Deleted, bool Retired, string Message);

public sealed class ConfigurationDeletionService(LedgerForgeDbContext dbContext)
{
    public Task<ConfigurationDeleteResult> DeleteLookupAsync(
        string type,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Lookup ID is required.", nameof(id));
        var selectedType = ResolveType(type);

        return selectedType switch
        {
            "BudgetSection" => DeleteManagedAsync(dbContext.BudgetSections, id, "Budget section", cancellationToken),
            "FinanceType" => DeleteManagedAsync(dbContext.FinanceTypes, id, "Finance type", cancellationToken),
            "FinanceCategory" => DeleteManagedAsync(dbContext.FinanceCategories, id, "Finance category", cancellationToken),
            "Department" => DeleteManagedAsync(dbContext.Departments, id, "Department", cancellationToken),
            "Location" => DeleteManagedAsync(dbContext.Locations, id, "Location", cancellationToken),
            "NeedLevel" => DeleteManagedAsync(dbContext.NeedLevels, id, "Need level", cancellationToken),
            "InternalCategory" => DeleteManagedAsync(dbContext.InternalCategories, id, "Internal category", cancellationToken),
            "Frequency" => DeleteManagedAsync(dbContext.Frequencies, id, "Frequency", cancellationToken),
            "Priority" => DeleteManagedAsync(dbContext.Priorities, id, "Priority", cancellationToken),
            "PurchaseType" => DeleteManagedAsync(dbContext.PurchaseTypes, id, "Purchase type", cancellationToken),
            "UnitOfMeasure" => DeleteManagedAsync(dbContext.UnitsOfMeasure, id, "Unit of measure", cancellationToken),
            "DocumentType" => DeleteManagedAsync(dbContext.DocumentTypes, id, "Document type", cancellationToken),
            "ApprovalStatus" => DeleteManagedAsync(dbContext.ApprovalStatuses, id, "Approval status", cancellationToken),
            "TransactionType" => DeleteManagedAsync(dbContext.TransactionTypes, id, "Transaction type", cancellationToken),
            "PurchaseOrderStatus" => DeleteManagedAsync(dbContext.PurchaseOrderStatuses, id, "Purchase-order status", cancellationToken),
            "InvoiceStatus" => DeleteManagedAsync(dbContext.InvoiceStatuses, id, "Invoice status", cancellationToken),
            "RenewalStatus" => DeleteManagedAsync(dbContext.RenewalStatuses, id, "Renewal status", cancellationToken),
            "ContractStatus" => DeleteManagedAsync(dbContext.ContractStatuses, id, "Contract status", cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    public Task<ConfigurationDeleteResult> DeleteFinanceAccountAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Finance account ID is required.", nameof(id));
        return DeleteManagedAsync(dbContext.FinanceAccounts, id, "Finance account", cancellationToken);
    }

    public async Task<ConfigurationDeleteResult> DeleteVendorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ArgumentException("Vendor ID is required.", nameof(id));
        var vendor = await dbContext.Vendors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Vendor was not found.");
        var label = vendor.Name;
        dbContext.Vendors.Remove(vendor);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(true, false, $"Vendor '{label}' was deleted because it had no historical references.");
        }
        catch (DbUpdateException exception) when (IsForeignKeyReference(exception))
        {
            dbContext.ChangeTracker.Clear();
            vendor = await dbContext.Vendors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Vendor was not found after the delete conflict.");
            vendor.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(false, true, $"Vendor '{label}' is referenced by financial history, so it was retired instead of deleted.");
        }
    }

    private async Task<ConfigurationDeleteResult> DeleteManagedAsync<TEntity>(
        DbSet<TEntity> set,
        Guid id,
        string label,
        CancellationToken cancellationToken)
        where TEntity : ManagedLookupEntity
    {
        var entity = await set.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"{label} was not found.");
        var displayName = entity.Name;
        set.Remove(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(true, false, $"{label} '{displayName}' was deleted because it had no historical references.");
        }
        catch (DbUpdateException exception) when (IsForeignKeyReference(exception))
        {
            dbContext.ChangeTracker.Clear();
            entity = await set.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException($"{label} was not found after the delete conflict.");
            entity.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(false, true, $"{label} '{displayName}' is referenced by historical records, so it was retired instead of deleted.");
        }
    }

    private static bool IsForeignKeyReference(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 547 }) return true;
        }
        return false;
    }

    private static string ResolveType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Lookup type is required.", nameof(type));
        var candidate = type.Trim();
        return LookupAdministrationService.Types
            .FirstOrDefault(x => string.Equals(x.Key, candidate, StringComparison.OrdinalIgnoreCase))?.Key
            ?? throw new ArgumentException("Unknown lookup type.", nameof(type));
    }
}
